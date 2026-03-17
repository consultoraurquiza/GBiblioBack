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

        // GET: api/prestamos/activos (Ideal para el Dashboard del bibliotecario)
        [HttpGet("activos")]
        public async Task<ActionResult<IEnumerable<Prestamo>>> GetPrestamosActivos()
        {
            // Traemos los préstamos e incluimos los datos del libro y del usuario para poder mostrarlos
            return await _context.Prestamos
                .Include(p => p.Libro)
                .Include(p => p.Usuario)
                .Where(p => p.Estado == EstadoPrestamo.Activo || p.Estado == EstadoPrestamo.Vencido)
                .ToListAsync();
        }

        // POST: api/prestamos/prestar
        [HttpPost("prestar")]
        public async Task<ActionResult> PrestarLibro([FromBody] PrestamoRequest request)
        {
            // 1. Buscamos si existen el libro y el usuario
            var libro = await _context.Libros.FindAsync(request.LibroId);
            var usuario = await _context.Usuarios.FindAsync(request.UsuarioId);

            if (libro == null || usuario == null) 
                return NotFound(new { mensaje = "Libro o Usuario no encontrado." });

            // 2. Validaciones de negocio (¡Acá está la clave!)
            if (libro.CantidadDisponible <= 0) 
                return BadRequest(new { mensaje = "No hay copias disponibles de este libro." });
                
            if (!usuario.PuedePedirPrestado) 
                return BadRequest(new { mensaje = "El usuario está inhabilitado para pedir libros." });

            // 3. Creamos el préstamo (7 días de plazo por defecto)
            var nuevoPrestamo = new Prestamo
            {
                LibroId = request.LibroId,
                UsuarioId = request.UsuarioId,
                FechaSalida = DateTime.UtcNow,
                FechaVencimiento = DateTime.UtcNow.AddDays(7), 
                Estado = EstadoPrestamo.Activo
            };

            // 4. Actualizamos el stock
            libro.CantidadDisponible -= 1;

            // 5. Guardamos todo en la base de datos
            _context.Prestamos.Add(nuevoPrestamo);
            await _context.SaveChangesAsync();

            return Ok(nuevoPrestamo);
        }

        // POST: api/prestamos/devolver/5
        [HttpPost("devolver/{id}")]
        public async Task<ActionResult> DevolverLibro(int id)
        {
            // Buscamos el préstamo y traemos también su libro asociado
            var prestamo = await _context.Prestamos
                .Include(p => p.Libro)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (prestamo == null) 
                return NotFound(new { mensaje = "Préstamo no encontrado." });

            if (prestamo.Estado == EstadoPrestamo.Devuelto) 
                return BadRequest(new { mensaje = "Este libro ya fue devuelto." });

            // Actualizamos los estados
            prestamo.FechaDevolucionReal = DateTime.UtcNow;
            prestamo.Estado = EstadoPrestamo.Devuelto;
            
            // Devolvemos el libro al stock
            prestamo.Libro.CantidadDisponible += 1;

            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Libro devuelto con éxito." });
        }
        // GET: api/prestamos/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Prestamo>> GetPrestamoPorId(int id)
        {
            var prestamo = await _context.Prestamos
                .Include(p => p.Libro)
                .Include(p => p.Usuario)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (prestamo == null) 
                return NotFound(new { mensaje = "Préstamo no encontrado." });

            return Ok(prestamo);
        }
    }

    // Un DTO (Data Transfer Object) simple para recibir los datos limpios de Next.js
    public class PrestamoRequest
    {
        public int LibroId { get; set; }
        public int UsuarioId { get; set; }
    }
}