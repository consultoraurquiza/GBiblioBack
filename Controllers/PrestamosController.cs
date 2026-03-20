using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PrestamosController : ControllerBase
    {
        private readonly BibliotecaContext _context;

        public PrestamosController(BibliotecaContext context)
        {
            _context = context;
        }

        // [HttpGet("activos")]
        // public async Task<ActionResult<IEnumerable<Prestamo>>> GetPrestamosActivos()
        // {
        //     return await _context.Prestamos
        //         .Include(p => p.Ejemplar)
        //             .ThenInclude(e => e.Libro) 
        //         // ELIMINAMOS EL INCLUDE DEL USUARIO
        //         .Where(p => p.Estado == EstadoPrestamo.Activo || p.Estado == EstadoPrestamo.Vencido)
        //         .ToListAsync();
        // }
        // GET: api/prestamos?filtro=activos
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Prestamo>>> GetPrestamosFiltrados([FromQuery] string filtro = "activos")
        {
            var query = _context.Prestamos
                .Include(p => p.Ejemplar)
                    .ThenInclude(e => e.Libro)
                .AsQueryable();

            switch (filtro.ToLower())
            {
                case "vencidos":
                    // Trae los vencidos explícitos o los activos cuya fecha ya se pasó
                    query = query.Where(p => 
                        p.Estado == EstadoPrestamo.Vencido || 
                        (p.Estado == EstadoPrestamo.Activo && p.FechaVencimiento < DateTime.UtcNow));
                    break;
                case "finalizados":
                    // Trae solo los devueltos
                    query = query.Where(p => p.Estado == EstadoPrestamo.Devuelto);
                    break;
                case "activos":
                default:
                    // Trae los prestados que siguen en la calle (al día o vencidos)
                    query = query.Where(p => p.Estado == EstadoPrestamo.Activo || p.Estado == EstadoPrestamo.Vencido);
                    break;
            }

            // Ordenamos para que los más recientes salgan primero en la tabla
            var prestamos = await query.OrderByDescending(p => p.FechaSalida).ToListAsync();
            
            return Ok(prestamos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Prestamo>> GetPrestamoPorId(int id)
        {
            var prestamo = await _context.Prestamos
                // ELIMINAMOS EL INCLUDE DEL USUARIO
                .Include(p => p.Ejemplar)
                    .ThenInclude(e => e.Libro) 
                .FirstOrDefaultAsync(p => p.Id == id);

            if (prestamo == null) 
                return NotFound(new { mensaje = "Préstamo no encontrado." });

            return Ok(prestamo);
        }

        // FUSIONAMOS EL VIEJO METODO CON NUESTRO NUEVO DTO MANUAL
        [HttpPost("prestar")]
        public async Task<ActionResult> PrestarLibro([FromBody] NuevoPrestamoManualDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.NombreLector))
                return BadRequest(new { mensaje = "El nombre de quien retira es obligatorio." });

            var ejemplar = await _context.Ejemplares.FindAsync(dto.EjemplarId);

            if (ejemplar == null) 
                return NotFound(new { mensaje = "Ejemplar físico no encontrado." });

            if (!ejemplar.DisponibleParaPrestamo) 
                return BadRequest(new { mensaje = "Este ejemplar en particular ya está prestado o en reparación." });

            var nuevoPrestamo = new Prestamo
            {
                EjemplarId = dto.EjemplarId,
                NombreLector = dto.NombreLector,   // <- CAMPO MANUAL
                CursoOAula = dto.CursoOAula,       // <- CAMPO MANUAL
                FechaSalida = DateTime.UtcNow,
                FechaVencimiento = DateTime.UtcNow.AddDays(7), 
                Estado = EstadoPrestamo.Activo
            };

            // Marcamos el ejemplar físico como no disponible
            ejemplar.DisponibleParaPrestamo = false;

            _context.Prestamos.Add(nuevoPrestamo);
            await _context.SaveChangesAsync();

            return Ok(nuevoPrestamo);
        }

        [HttpPost("devolver/{id}")]
        public async Task<ActionResult> DevolverLibro(int id)
        {
            var prestamo = await _context.Prestamos
                .Include(p => p.Ejemplar)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (prestamo == null) 
                return NotFound(new { mensaje = "Préstamo no encontrado." });

            if (prestamo.Estado == EstadoPrestamo.Devuelto) 
                return BadRequest(new { mensaje = "Este libro ya fue devuelto." });

            prestamo.FechaDevolucionReal = DateTime.UtcNow;
            prestamo.Estado = EstadoPrestamo.Devuelto;
            
            // Volvemos a habilitar el ejemplar físico
            prestamo.Ejemplar.DisponibleParaPrestamo = true;

            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Ejemplar devuelto con éxito." });
        }
    }

    // DTO REVISADO: Usamos EjemplarId para saber exactamente qué copia se llevan
    public class NuevoPrestamoManualDTO
    {
        public int EjemplarId { get; set; } 
        public string NombreLector { get; set; } = string.Empty;
        public string CursoOAula { get; set; } = string.Empty;
    }
}