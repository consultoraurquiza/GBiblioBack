using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LibrosController : ControllerBase
    {
        private readonly BibliotecaContext _context;

        public LibrosController(BibliotecaContext context)
        {
            _context = context;
        }

        // GET: api/libros
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Libro>>> GetLibros()
        {
            // Incluimos los ejemplares físicos y los tags para que Next.js tenga todo armado
            return await _context.Libros
                .Include(l => l.Ejemplares)
                .Include(l => l.Tags)
                .ToListAsync();
        }

        // GET: api/libros/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Libro>> GetLibroPorId(int id)
        {
            var libro = await _context.Libros
                .Include(l => l.Ejemplares)
                .Include(l => l.Tags)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (libro == null) return NotFound(new { mensaje = "Libro no encontrado." });

            return Ok(libro);
        }

        // GET: api/libros/buscar/borges
        [HttpGet("buscar/{texto}")]
        public async Task<ActionResult<IEnumerable<Libro>>> BuscarLibro(string texto)
        {
            // 1. Rompemos la frase en palabras (ej: "Ficción Adulto" -> ["ficción", "adulto"])
            var terminos = texto.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            // 2. Preparamos la consulta base
            var query = _context.Libros
                .Include(l => l.Ejemplares)
                .Include(l => l.Tags)
                .AsQueryable();

            // 3. Magia: Por CADA palabra, filtramos los resultados. 
            // Al encadenar los "Where", Entity Framework aplica automáticamente un "AND"
            foreach (var termino in terminos)
            {
                query = query.Where(l => 
                    l.Titulo.ToLower().Contains(termino) || 
                    l.AutorPrincipal.ToLower().Contains(termino) ||
                    (l.Isbn != null && l.Isbn.ToLower().Contains(termino)) ||
                    (l.Clasificacion != null && l.Clasificacion.ToLower().Contains(termino)) ||
                    (l.CodigoCutter != null && l.CodigoCutter.ToLower().Contains(termino)) ||
                    
                    // Busca adentro de los tags
                    l.Tags.Any(t => t.Nombre.ToLower().Contains(termino)) ||
                    
                    // Busca el número de inventario
                    l.Ejemplares.Any(e => e.NumeroInventario.ToLower() == termino)
                );
            }

            // 4. Recién acá vamos a la base de datos a buscar la información
            var libros = await query.ToListAsync();

            if (!libros.Any())
            {
                return NotFound(new { mensaje = "No se encontraron libros que coincidan con todos los términos." });
            }

            return Ok(libros);
        }
        
        // POST: api/libros
        [HttpPost]
        public async Task<ActionResult<Libro>> PostLibro([FromBody] LibroCreacionDTO dto)
        {
            var nuevoLibro = new Libro
            {
                Titulo = dto.Titulo,
                Subtitulo = dto.Subtitulo,
                AutorPrincipal = dto.AutorPrincipal,
                Editorial = dto.Editorial,
                AnioPublicacion = dto.AnioPublicacion,
                Isbn = dto.Isbn,
                Clasificacion = dto.Clasificacion,
                CodigoCutter = dto.CodigoCutter
            };

            // 1. Armar los ejemplares físicos
            foreach (var ej in dto.Ejemplares)
            {
                nuevoLibro.Ejemplares.Add(new Ejemplar 
                { 
                    NumeroInventario = ej.NumeroInventario,
                    Observaciones = ej.Observaciones,
                    DisponibleParaPrestamo = true
                });
            }

            // 2. Armar los Tags (Acá está la magia)
            foreach (var nombreTag in dto.Tags)
            {
                var tagLimpio = nombreTag.Trim();
                // Buscamos si el tag ya existe en la base de datos (ignorando mayúsculas)
                var tagExistente = await _context.Tags
                    .FirstOrDefaultAsync(t => t.Nombre.ToLower() == tagLimpio.ToLower());
                
                if (tagExistente != null)
                {
                    nuevoLibro.Tags.Add(tagExistente); // Lo vinculamos
                }
                else
                {
                    nuevoLibro.Tags.Add(new Tag { Nombre = tagLimpio }); // Lo creamos de cero
                }
            }

            _context.Libros.Add(nuevoLibro);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetLibros), new { id = nuevoLibro.Id }, nuevoLibro);
        }
    }

    // --- AGREGAR ESTAS DOS CLASES AL FINAL DEL ARCHIVO ---
    // Son los "moldes" para recibir los datos limpios desde Next.js
    public class LibroCreacionDTO
    {
        public string Titulo { get; set; } = string.Empty;
        public string? Subtitulo { get; set; }
        public string AutorPrincipal { get; set; } = string.Empty;
        public string? Editorial { get; set; }
        public string? AnioPublicacion { get; set; }
        public string? Isbn { get; set; }
        public string? Clasificacion { get; set; }
        public string? CodigoCutter { get; set; }
        
        public List<EjemplarDTO> Ejemplares { get; set; } = new();
        public List<string> Tags { get; set; } = new();
    }

    public class EjemplarDTO
    {
        public string NumeroInventario { get; set; } = string.Empty;
        public string? Observaciones { get; set; }
    }

    
}