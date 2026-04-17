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

        // GET: api/prestamos?filtro=activos
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Prestamo>>> GetPrestamosFiltrados([FromQuery] string filtro = "activos")
        {
            var query = _context.Prestamos
                .Include(p => p.Ejemplar)
                    .ThenInclude(e => e.Libro)
                .Include(p => p.Usuario) // 👈 VOLVEMOS A INCLUIR AL USUARIO
                    .ThenInclude(e => e.Grupo)
                .AsQueryable();

            switch (filtro.ToLower())
            {
                case "vencidos":
                    query = query.Where(p =>
                        p.Estado == EstadoPrestamo.Vencido ||
                        (p.Estado == EstadoPrestamo.Activo && p.FechaVencimiento < DateTime.UtcNow));
                    break;
                case "finalizados":
                    query = query.Where(p => p.Estado == EstadoPrestamo.Devuelto);
                    break;
                case "activos":
                default:
                    query = query.Where(p => p.Estado == EstadoPrestamo.Activo || p.Estado == EstadoPrestamo.Vencido);
                    break;
            }

            var prestamos = await query.OrderByDescending(p => p.FechaSalida).ToListAsync();

            return Ok(prestamos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Prestamo>> GetPrestamoPorId(int id)
        {
            var prestamo = await _context.Prestamos
                .Include(p => p.Ejemplar)
                    .ThenInclude(e => e.Libro)
                .Include(p => p.Usuario) // 👈 VOLVEMOS A INCLUIR AL USUARIO
                .FirstOrDefaultAsync(p => p.Id == id);

            if (prestamo == null)
                return NotFound(new { mensaje = "Préstamo no encontrado." });

            return Ok(prestamo);
        }

        [HttpPost("prestar")]
        public async Task<ActionResult> PrestarLibro([FromBody] NuevoPrestamoDTO dto)
        {


            // 1. VALIDACIÓN HÍBRIDA: Tiene que haber un ID de usuario O un nombre escrito a mano
            if (!dto.UsuarioId.HasValue && string.IsNullOrWhiteSpace(dto.NombreManual))
                return BadRequest(new { mensaje = "Debe seleccionar un usuario registrado o ingresar un nombre manualmente." });

            // --- NUEVA VALIDACIÓN DE LÍMITE DE LIBROS ---
            var configSistema = await _context.Configuracion.FirstOrDefaultAsync();

            if (dto.UsuarioId.HasValue)
            {
                var lector = await _context.Usuarios
                    .Include(u => u.Grupo)
                    .FirstOrDefaultAsync(u => u.Id == dto.UsuarioId);

                if (lector != null)
                {
                    // 🚨 VALIDACIÓN 1: ¿El grupo del lector es un "Archivo Muerto"?
                    if (lector.Grupo != null && !lector.Grupo.HabilitadoParaPrestamos)
                    {
                        return BadRequest(new { mensaje = $"Operación denegada: Los miembros del grupo '{lector.Grupo.Nombre}' no están habilitados para retirar material de la escuela." });
                    }

                    // (Opcional) VALIDACIÓN 2: Si tenés un switch individual por alumno
                     if (!lector.PuedePedirPrestado) return BadRequest(new { mensaje = "Este alumno está sancionado individualmente." });
                }



                int limiteMaximo = configSistema?.MaxLibrosPorPersona ?? 3; // 3 por defecto si no hay config

                // Contamos cuántos libros tiene sin devolver actualmente
                var prestamosActivos = await _context.Prestamos
                    .CountAsync(p => p.UsuarioId == dto.UsuarioId && p.FechaDevolucionReal == null);

                if (prestamosActivos >= limiteMaximo)
                {
                    return BadRequest(new { mensaje = $"Límite excedido: El usuario ya tiene {prestamosActivos} libros en su poder. El máximo permitido es {limiteMaximo}." });
                }
            }

            var ejemplar = await _context.Ejemplares.FindAsync(dto.EjemplarId);

            if (ejemplar == null)
                return NotFound(new { mensaje = "Ejemplar físico no encontrado." });

            if (!ejemplar.DisponibleParaPrestamo)
                return BadRequest(new { mensaje = "Este ejemplar en particular ya está prestado o en reparación." });

            // 2. DÍAS DINÁMICOS: Traemos la configuración de la base de datos
            //var configSistema = await _context.Configuracion.FirstOrDefaultAsync();
            int diasDePrestamo = configSistema?.DiasPrestamo ?? 7; // Si falla algo, 7 días por defecto

            var nuevoPrestamo = new Prestamo
            {
                EjemplarId = dto.EjemplarId,
                UsuarioId = dto.UsuarioId,           // <- ID si es registrado
                NombreLector = dto.NombreManual,     // <- Texto si es rápido
                CursoOAula = dto.CursoManual,       // <- Texto si es rápido
                TelefonoManual = dto.TelefonoManual, // <- Texto si es rápido
                FechaSalida = DateTime.UtcNow,
                FechaVencimiento = DateTime.UtcNow.AddDays(diasDePrestamo), // <- Calculado dinámicamente
                Estado = EstadoPrestamo.Activo
            };

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

            prestamo.Ejemplar.DisponibleParaPrestamo = true;

            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Ejemplar devuelto con éxito." });
        }
        // GET: Obtener el historial de préstamos de un usuario específico
        [HttpGet("usuario/{usuarioId}")]
        public async Task<ActionResult> GetHistorialPorUsuario(int usuarioId)
        {
            var historial = await _context.Prestamos
                .Include(p => p.Ejemplar)
                    .ThenInclude(e => e.Libro)
                .Where(p => p.UsuarioId == usuarioId)
                .OrderByDescending(p => p.FechaSalida) // Los más recientes primero
                .Select(p => new
                {
                    p.Id,
                    Titulo = p.Ejemplar.Libro.Titulo,
                    Inventario = p.Ejemplar.NumeroInventario,
                    FechaSalida = p.FechaSalida,
                    FechaVencimiento = p.FechaVencimiento,
                    FechaDevolucion = p.FechaDevolucionReal,
                    Estado = p.FechaDevolucionReal != null ? "Devuelto" :
                            (p.FechaVencimiento < DateTime.UtcNow ? "Vencido" : "Activo")
                })
                .ToListAsync();

            return Ok(historial);
        }
    }



    // DTO ACTUALIZADO PARA SOPORTAR EL MODO HÍBRIDO
    public class NuevoPrestamoDTO
    {
        public int EjemplarId { get; set; }
        public int? UsuarioId { get; set; }
        public string? NombreManual { get; set; }
        public string? CursoManual { get; set; }
        public string? TelefonoManual { get; set; }
    }
}