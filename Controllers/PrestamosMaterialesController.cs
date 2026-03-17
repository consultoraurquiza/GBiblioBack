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
                .Include(p => p.Usuario)
                .Where(p => p.Estado == EstadoPrestamo.Activo || p.Estado == EstadoPrestamo.Vencido)
                .ToListAsync();
        }

        [HttpPost("prestar")]
        public async Task<ActionResult> PrestarMaterial([FromBody] PrestamoMaterialRequest request)
        {
            var material = await _context.Materiales.FindAsync(request.MaterialId);
            var usuario = await _context.Usuarios.FindAsync(request.UsuarioId);

            if (material == null || usuario == null) 
                return NotFound(new { mensaje = "Material o Usuario no encontrado." });

            if (material.CantidadDisponible <= 0) 
                return BadRequest(new { mensaje = "No hay unidades disponibles de este material." });
                
            if (!usuario.PuedePedirPrestado) 
                return BadRequest(new { mensaje = "El usuario está inhabilitado." });

            var nuevoPrestamo = new PrestamoMaterial
            {
                MaterialId = request.MaterialId,
                UsuarioId = request.UsuarioId,
                FechaSalida = DateTime.UtcNow,
                FechaVencimiento = DateTime.UtcNow.AddDays(1), // Generalmente se devuelven rápido
                Estado = EstadoPrestamo.Activo
            };

            material.CantidadDisponible -= 1;
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
            prestamo.Material.CantidadDisponible += 1;

            await _context.SaveChangesAsync();
            return Ok(new { mensaje = "Material devuelto." });
        }
    }

    public class PrestamoMaterialRequest
    {
        public int MaterialId { get; set; }
        public int UsuarioId { get; set; }
    }
}