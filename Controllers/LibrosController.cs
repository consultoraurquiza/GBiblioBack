using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;

namespace Backend.Controllers
{
    // Esta ruta significa que vas a acceder a esto desde: http://localhost:puerto/api/libros
    [Route("api/[controller]")]
    [ApiController]
    public class LibrosController : ControllerBase
    {
        private readonly BibliotecaContext _context;

        // Inyectamos la base de datos
        public LibrosController(BibliotecaContext context)
        {
            _context = context;
        }

        // GET: api/libros (Te devuelve todos los libros)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Libro>>> GetLibros()
        {
            return await _context.Libros.ToListAsync();
        }

        // POST: api/libros (Agrega un libro nuevo)
        [HttpPost]
        public async Task<ActionResult<Libro>> PostLibro(Libro libro)
        {
            _context.Libros.Add(libro);
            await _context.SaveChangesAsync();

            // Te devuelve el libro recién creado con su ID generado
            return CreatedAtAction(nameof(GetLibros), new { id = libro.Id }, libro);
        }
        [HttpGet("buscar/{texto}")]
        public async Task<ActionResult<IEnumerable<Libro>>> BuscarLibro(string texto)
        {
         // Pasamos todo a minúsculas para que la búsqueda no sea sensible a mayúsculas
            var busqueda = texto.ToLower();

            var libros = await _context.Libros
                .Where(l => l.Titulo.ToLower().Contains(busqueda) || l.Isbn == texto)
                .ToListAsync();

            if (!libros.Any())
         {
             return NotFound(new { mensaje = "No se encontraron libros con ese título o código." });
          }

            return Ok(libros);
        }
    }
}