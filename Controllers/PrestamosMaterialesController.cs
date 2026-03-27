using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PrestamosMaterialesController : ControllerBase
    {
        private readonly BibliotecaContext _context;

        public PrestamosMaterialesController(BibliotecaContext context)
        {
            _context = context;
        }

        [HttpGet("activos")]
        public async Task<ActionResult<IEnumerable<PrestamoMaterial>>> GetPrestamosActivos()
        {
            return await _context.PrestamosMateriales
                .Include(p => p.Material)
                // Le sacamos el .Include(p => p.Usuario) porque ya no existe
                .Where(p => p.Estado == EstadoPrestamo.Activo || p.Estado == EstadoPrestamo.Vencido)
                .ToListAsync();
        }

        [HttpPost("prestar")]
        public async Task<ActionResult> PrestarMaterial([FromBody] PrestamoMaterialRequest request)
        {
            if (request.Cantidad <= 0)
                return BadRequest(new { mensaje = "La cantidad debe ser mayor a cero." });

            if (string.IsNullOrWhiteSpace(request.NombreSolicitante))
                return BadRequest(new { mensaje = "Debes ingresar a quién se le presta el equipo." });

            var material = await _context.Materiales.FindAsync(request.MaterialId);

            if (material == null) 
                return NotFound(new { mensaje = "Material no encontrado." });

            if (material.CantidadDisponible < request.Cantidad) 
                return BadRequest(new { mensaje = $"Stock insuficiente. Solo hay {material.CantidadDisponible} unidades." });

            var nuevoPrestamo = new PrestamoMaterial
            {
                MaterialId = request.MaterialId,
                NombreSolicitante = request.NombreSolicitante, // Guardamos el texto libre
                CantidadPrestada = request.Cantidad,
                FechaSalida = DateTime.UtcNow,
                FechaVencimiento = DateTime.UtcNow.AddDays(1), 
                Estado = EstadoPrestamo.Activo
            };

            material.CantidadDisponible -= request.Cantidad;
            
            _context.PrestamosMateriales.Add(nuevoPrestamo);
            await _context.SaveChangesAsync();

            return Ok(nuevoPrestamo);
        }

        [HttpPost("devolver/{id}")]
        public async Task<ActionResult> DevolverMaterial(int id)
        {
            var prestamo = await _context.PrestamosMateriales
                .Include(p => p.Material)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (prestamo == null) return NotFound(new { mensaje = "Préstamo no encontrado." });
            if (prestamo.Estado == EstadoPrestamo.Devuelto) return BadRequest(new { mensaje = "Ya fue devuelto." });

            prestamo.FechaDevolucionReal = DateTime.UtcNow;
            prestamo.Estado = EstadoPrestamo.Devuelto;
            prestamo.Material.CantidadDisponible += prestamo.CantidadPrestada;

            await _context.SaveChangesAsync();
            return Ok(new { mensaje = "Material devuelto correctamente." });
        }
    }

    public class PrestamoMaterialRequest
    {
        public int MaterialId { get; set; }
        
        // Cambiamos UsuarioId por el texto
        public string NombreSolicitante { get; set; } = string.Empty; 
        
        public int Cantidad { get; set; } = 1; 
    }
}