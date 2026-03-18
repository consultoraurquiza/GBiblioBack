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
        // PUT: api/libros/5
        [HttpPut("{id}")]
        public async Task<IActionResult> EditarLibro(int id, [FromBody] LibroEdicionDTO dto)
        {
            if (id != dto.Id) return BadRequest(new { mensaje = "El ID no coincide." });

            // Buscamos el libro con todas sus relaciones cargadas
            var libro = await _context.Libros
                .Include(l => l.Ejemplares)
                .Include(l => l.Tags)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (libro == null) return NotFound(new { mensaje = "Libro no encontrado." });

            // 1. Actualizar datos básicos (MARC21)
            libro.Titulo = dto.Titulo;
            libro.Subtitulo = dto.Subtitulo;
            libro.AutorPrincipal = dto.AutorPrincipal;
            libro.Editorial = dto.Editorial;
            libro.AnioPublicacion = dto.AnioPublicacion;
            libro.Isbn = dto.Isbn;
            libro.Clasificacion = dto.Clasificacion;
            libro.CodigoCutter = dto.CodigoCutter;

            // 2. Actualizar Tags (Borramos las viejas relaciones y armamos las nuevas)
            libro.Tags.Clear();
            foreach (var nombreTag in dto.Tags)
            {
                var tagLimpio = nombreTag.Trim();
                var tagExistente = await _context.Tags.FirstOrDefaultAsync(t => t.Nombre.ToLower() == tagLimpio.ToLower());
                
                if (tagExistente != null) libro.Tags.Add(tagExistente);
                else libro.Tags.Add(new Tag { Nombre = tagLimpio });
            }

            // 3. Actualizar Ejemplares (El inventario físico)
            // A) Eliminar los ejemplares que el usuario quitó en la pantalla
            var idsRecibidos = dto.Ejemplares.Where(e => e.Id.HasValue).Select(e => e.Id.Value).ToList();
            var ejemplaresABorrar = libro.Ejemplares.Where(e => !idsRecibidos.Contains(e.Id)).ToList();
            _context.Ejemplares.RemoveRange(ejemplaresABorrar);

            // B) Actualizar los existentes o agregar los nuevos
            foreach (var ejDto in dto.Ejemplares)
            {
                if (ejDto.Id.HasValue) 
                {
                    // Actualizamos un ejemplar que ya existía (ej: se rompió y cambiamos el estado)
                    var ejExistente = libro.Ejemplares.FirstOrDefault(e => e.Id == ejDto.Id.Value);
                    if (ejExistente != null)
                    {
                        ejExistente.NumeroInventario = ejDto.NumeroInventario;
                        ejExistente.Observaciones = ejDto.Observaciones;
                        ejExistente.DisponibleParaPrestamo = ejDto.DisponibleParaPrestamo;
                    }
                }
                else 
                {
                    // Es un ejemplar nuevo que agregaron haciendo clic en "+ Agregar Copia"
                    libro.Ejemplares.Add(new Ejemplar 
                    { 
                        NumeroInventario = ejDto.NumeroInventario,
                        Observaciones = ejDto.Observaciones,
                        DisponibleParaPrestamo = ejDto.DisponibleParaPrestamo
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Ok(libro);
        }

        // POST: api/libros/sembrar-datos
        // ¡IMPORTANTE! Usar solo para pruebas.
        [HttpPost("sembrar-datos")]
        public async Task<IActionResult> SembrarDatosDePrueba()
        {
            // Verificamos si ya corrimos este script para no duplicar todo
            if (await _context.Libros.AnyAsync(l => l.Titulo == "Dune")) 
                return BadRequest(new { mensaje = "Los datos de prueba ya fueron cargados previamente." });

            // 1. Creamos un diccionario de Tags para reutilizarlos en varios libros a la vez
            var tags = new Dictionary<string, Tag>
            {
                { "Fantasía", new Tag { Nombre = "Fantasía" } },
                { "Ciencia Ficción", new Tag { Nombre = "Ciencia Ficción" } },
                { "Adulto", new Tag { Nombre = "Adulto" } },
                { "Juvenil", new Tag { Nombre = "Juvenil" } },
                { "Infantil", new Tag { Nombre = "Infantil" } },
                { "Historia", new Tag { Nombre = "Historia" } },
                { "Argentina", new Tag { Nombre = "Argentina" } },
                { "Tecnología", new Tag { Nombre = "Tecnología" } },
                { "Programación", new Tag { Nombre = "Programación" } },
                { "Policial", new Tag { Nombre = "Policial" } },
                { "Clásico", new Tag { Nombre = "Clásico" } }
            };

            // Guardamos los tags primero
            foreach (var tag in tags.Values) _context.Tags.Add(tag);

            // 2. Creamos 15 libros estratégicos con cruces de tags interesantes
            var librosDePrueba = new List<Libro>
            {
                new Libro {
                    Titulo = "El Señor de los Anillos", Subtitulo = "La Comunidad del Anillo", AutorPrincipal = "Tolkien, J.R.R.",
                    Editorial = "Minotauro", AnioPublicacion = "1954", Isbn = "978-84-450", Clasificacion = "823", CodigoCutter = "T649",
                    Tags = new List<Tag> { tags["Fantasia"], tags["Adulto"], tags["Clásico"] },
                    Ejemplares = new List<Ejemplar> {
                        new Ejemplar { NumeroInventario = "INV-1001", DisponibleParaPrestamo = true },
                        new Ejemplar { NumeroInventario = "INV-1002", DisponibleParaPrestamo = true, Observaciones = "Lomo gastado" }
                    }
                },
                new Libro {
                    Titulo = "Harry Potter y la Piedra Filosofal", AutorPrincipal = "Rowling, J.K.",
                    Editorial = "Salamandra", AnioPublicacion = "1997", Clasificacion = "823", CodigoCutter = "R883",
                    Tags = new List<Tag> { tags["Fantasia"], tags["Juvenil"] },
                    Ejemplares = new List<Ejemplar> {
                        new Ejemplar { NumeroInventario = "INV-1003", DisponibleParaPrestamo = true },
                        new Ejemplar { NumeroInventario = "INV-1004", DisponibleParaPrestamo = false, Observaciones = "En reparación" }
                    }
                },
                new Libro {
                    Titulo = "Dune", AutorPrincipal = "Herbert, Frank",
                    Editorial = "Acervo", AnioPublicacion = "1965", Clasificacion = "813", CodigoCutter = "H536",
                    Tags = new List<Tag> { tags["Ciencia Ficción"], tags["Adulto"], tags["Clásico"] },
                    Ejemplares = new List<Ejemplar> { new Ejemplar { NumeroInventario = "INV-1005", DisponibleParaPrestamo = true } }
                },
                new Libro {
                    Titulo = "Fahrenheit 451", AutorPrincipal = "Bradbury, Ray",
                    Editorial = "Minotauro", AnioPublicacion = "1953", Clasificacion = "813", CodigoCutter = "B798",
                    Tags = new List<Tag> { tags["Ciencia Ficcion"], tags["Adulto"] },
                    Ejemplares = new List<Ejemplar> { new Ejemplar { NumeroInventario = "INV-1006", DisponibleParaPrestamo = true } }
                },
                new Libro {
                    Titulo = "Breve Historia de la Argentina", AutorPrincipal = "Romero, Jose Luis",
                    Editorial = "FCE", AnioPublicacion = "2001", Clasificacion = "982", CodigoCutter = "R763",
                    Tags = new List<Tag> { tags["Historia"], tags["Argentina"] },
                    Ejemplares = new List<Ejemplar> {
                        new Ejemplar { NumeroInventario = "INV-1007", DisponibleParaPrestamo = true },
                        new Ejemplar { NumeroInventario = "INV-1008", DisponibleParaPrestamo = true, Observaciones = "Subrayado con lápiz" }
                    }
                },
                new Libro {
                    Titulo = "Clean Code", Subtitulo = "A Handbook of Agile Software Craftsmanship", AutorPrincipal = "Martin, Robert C.",
                    Editorial = "Prentice Hall", AnioPublicacion = "2008", Clasificacion = "005.1", CodigoCutter = "M383",
                    Tags = new List<Tag> { tags["Tecnologia"], tags["Programacion"] },
                    Ejemplares = new List<Ejemplar> { new Ejemplar { NumeroInventario = "INV-1009", DisponibleParaPrestamo = true } }
                },
                new Libro {
                    Titulo = "C# 10 in a Nutshell", AutorPrincipal = "Albahari, Joseph",
                    Editorial = "OReilly", AnioPublicacion = "2022", Clasificacion = "005.13", CodigoCutter = "A326",
                    Tags = new List<Tag> { tags["Tecnologia"], tags["Programacion"] },
                    Ejemplares = new List<Ejemplar> { new Ejemplar { NumeroInventario = "INV-1010", DisponibleParaPrestamo = true } }
                },
                new Libro {
                    Titulo = "El Aleph", AutorPrincipal = "Borges, Jorge Luis",
                    Editorial = "Alianza", AnioPublicacion = "1949", Clasificacion = "863", CodigoCutter = "B732",
                    Tags = new List<Tag> { tags["Fantasia"], tags["Clasico"], tags["Argentina"] },
                    Ejemplares = new List<Ejemplar> { new Ejemplar { NumeroInventario = "INV-1011", DisponibleParaPrestamo = true } }
                },
                new Libro {
                    Titulo = "Historia de San Martín", AutorPrincipal = "Mitre, Bartolomé",
                    Editorial = "Sudamericana", AnioPublicacion = "1887", Clasificacion = "982", CodigoCutter = "M684",
                    Tags = new List<Tag> { tags["Historia"], tags["Argentina"], tags["Clasico"] },
                    Ejemplares = new List<Ejemplar> { new Ejemplar { NumeroInventario = "INV-1012", DisponibleParaPrestamo = true } }
                },
                new Libro {
                    Titulo = "El Patito Feo", AutorPrincipal = "Andersen, Hans Christian",
                    Editorial = "Juventud", AnioPublicacion = "1843", Clasificacion = "398.2", CodigoCutter = "A544",
                    Tags = new List<Tag> { tags["Infantil"], tags["Clasico"] },
                    Ejemplares = new List<Ejemplar> { new Ejemplar { NumeroInventario = "INV-1013", DisponibleParaPrestamo = true } }
                },
                new Libro {
                    Titulo = "Diez Negritos", AutorPrincipal = "Christie, Agatha",
                    Editorial = "Espasa", AnioPublicacion = "1939", Clasificacion = "823", CodigoCutter = "C462",
                    Tags = new List<Tag> { tags["Policial"], tags["Clasico"] },
                    Ejemplares = new List<Ejemplar> { new Ejemplar { NumeroInventario = "INV-1014", DisponibleParaPrestamo = true } }
                },
                new Libro {
                    Titulo = "Estudio en Escarlata", AutorPrincipal = "Doyle, Arthur Conan",
                    Editorial = "Alianza", AnioPublicacion = "1887", Clasificacion = "823", CodigoCutter = "D754",
                    Tags = new List<Tag> { tags["Policial"], tags["Adulto"], tags["Clasico"] },
                    Ejemplares = new List<Ejemplar> { new Ejemplar { NumeroInventario = "INV-1015", DisponibleParaPrestamo = true } }
                },
                new Libro {
                    Titulo = "El Eternauta", AutorPrincipal = "Oesterheld, Héctor Germán",
                    Editorial = "Doedytores", AnioPublicacion = "1957", Clasificacion = "741.5", CodigoCutter = "O298",
                    Tags = new List<Tag> { tags["Ciencia Ficcion"], tags["Argentina"], tags["Juvenil"] },
                    Ejemplares = new List<Ejemplar> { 
                        new Ejemplar { NumeroInventario = "INV-1016", DisponibleParaPrestamo = true },
                        new Ejemplar { NumeroInventario = "INV-1017", DisponibleParaPrestamo = true }
                    }
                },
                new Libro {
                    Titulo = "Cien Años de Soledad", AutorPrincipal = "García Márquez, Gabriel",
                    Editorial = "Sudamericana", AnioPublicacion = "1967", Clasificacion = "863", CodigoCutter = "G216",
                    Tags = new List<Tag> { tags["Fantasia"], tags["Adulto"], tags["Clasico"] },
                    Ejemplares = new List<Ejemplar> { new Ejemplar { NumeroInventario = "INV-1018", DisponibleParaPrestamo = true } }
                },
                new Libro {
                    Titulo = "Aprende SQL en un fin de semana", AutorPrincipal = "Pérez, Antonio",
                    Editorial = "Autopublicado", AnioPublicacion = "2020", Clasificacion = "005.74", CodigoCutter = "P438",
                    Tags = new List<Tag> { tags["Tecnologia"], tags["Programacion"], tags["Juvenil"] },
                    Ejemplares = new List<Ejemplar> { new Ejemplar { NumeroInventario = "INV-1019", DisponibleParaPrestamo = true } }
                }
            };

            // Mandamos todo a PostgreSQL de un solo golpe
            _context.Libros.AddRange(librosDePrueba);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "¡15 libros cargados! Tenés inventario, tags combinados y ejemplares físicos listos." });
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
    public class LibroEdicionDTO
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string? Subtitulo { get; set; }
        public string AutorPrincipal { get; set; } = string.Empty;
        public string? Editorial { get; set; }
        public string? AnioPublicacion { get; set; }
        public string? Isbn { get; set; }
        public string? Clasificacion { get; set; }
        public string? CodigoCutter { get; set; }
        
        public List<EjemplarEdicionDTO> Ejemplares { get; set; } = new();
        public List<string> Tags { get; set; } = new();
    }

    public class EjemplarEdicionDTO
    {
        public int? Id { get; set; } // Opcional: Si viene nulo es porque es un libro nuevo
        public string NumeroInventario { get; set; } = string.Empty;
        public string? Observaciones { get; set; }
        public bool DisponibleParaPrestamo { get; set; } 
    }

    
}