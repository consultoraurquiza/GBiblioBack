using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;

using System.Net.Http;
using System.Text.Json;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LibrosController : ControllerBase
    {
        private readonly BibliotecaContext _context;
        private readonly IImagenService _imagenService;

        public LibrosController(BibliotecaContext context, IImagenService imagenService)
        {
            _context = context;
            _imagenService = imagenService;
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
        public async Task<ActionResult<Libro>> PostLibro([FromForm] LibroCreacionDTO dto) // <--- OJO ACÁ: [FromForm]
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
                CodigoCutter = dto.CodigoCutter,
                ReseniaSinopsis = dto.ReseniaSinopsis,
                CantidadPaginas = dto.CantidadPaginas,
                PortadaUrl = dto.PortadaUrl,
                UsarPortadaLocal = dto.UsarPortadaLocal
            };

            // 1. Armar los ejemplares
            foreach (var ej in dto.Ejemplares)
            {
                nuevoLibro.Ejemplares.Add(new Ejemplar 
                { 
                    NumeroInventario = ej.NumeroInventario,
                    Observaciones = ej.Observaciones,
                    DisponibleParaPrestamo = true
                });
            }

            // 2. Armar los Tags
            foreach (var nombreTag in dto.Tags)
            {
                var tagLimpio = nombreTag.Trim();
                var tagExistente = await _context.Tags.FirstOrDefaultAsync(t => t.Nombre.ToLower() == tagLimpio.ToLower());
                
                if (tagExistente != null) nuevoLibro.Tags.Add(tagExistente);
                else nuevoLibro.Tags.Add(new Tag { Nombre = tagLimpio });
            }

            // 3. PRIMER GUARDADO: Guardamos para obtener el ID real de la base de datos
            _context.Libros.Add(nuevoLibro);
            await _context.SaveChangesAsync();

            // 4. MAGIA DE IMÁGENES: Ahora que tenemos el ID (ej: nuevoLibro.Id = 17), procesamos la foto
            if (dto.UsarPortadaLocal)
            {
                if (dto.ArchivoPortada != null)
                {
                    // Si el usuario subió una foto desde su PC
                    nuevoLibro.PortadaLocalUrl = await _imagenService.GuardarImagenSubida(dto.ArchivoPortada, nuevoLibro.Id);
                }
                else if (!string.IsNullOrWhiteSpace(dto.PortadaUrl))
                {
                    // Si quiere que C# descargue la foto de Google automáticamente
                    nuevoLibro.PortadaLocalUrl = await _imagenService.DescargarImagenDesdeUrl(dto.PortadaUrl, nuevoLibro.Id);
                }

                // Si logramos guardar la imagen localmente, actualizamos el registro
                if (!string.IsNullOrEmpty(nuevoLibro.PortadaLocalUrl))
                {
                    await _context.SaveChangesAsync();
                }
            }

            return CreatedAtAction(nameof(GetLibros), new { id = nuevoLibro.Id }, nuevoLibro);
        }

        // PUT: api/libros/5
        [HttpPut("{id}")]
        public async Task<IActionResult> EditarLibro(int id, [FromForm] LibroEdicionDTO dto) // <--- [FromForm]
        {
            if (id != dto.Id) return BadRequest(new { mensaje = "El ID no coincide." });

            var libro = await _context.Libros
                .Include(l => l.Ejemplares)
                .Include(l => l.Tags)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (libro == null) return NotFound(new { mensaje = "Libro no encontrado." });

            // 1. Actualizar datos básicos
            libro.Titulo = dto.Titulo;
            libro.Subtitulo = dto.Subtitulo;
            libro.AutorPrincipal = dto.AutorPrincipal;
            libro.Editorial = dto.Editorial;
            libro.AnioPublicacion = dto.AnioPublicacion;
            libro.Isbn = dto.Isbn;
            libro.Clasificacion = dto.Clasificacion;
            libro.CodigoCutter = dto.CodigoCutter;
            libro.ReseniaSinopsis = dto.ReseniaSinopsis;
            libro.CantidadPaginas = dto.CantidadPaginas;
            libro.PortadaUrl = dto.PortadaUrl;
            libro.UsarPortadaLocal = dto.UsarPortadaLocal;

            // 2. MAGIA DE IMÁGENES (Actualización)
            if (dto.UsarPortadaLocal)
            {
                if (dto.ArchivoPortada != null)
                {
                    // Si subió un archivo nuevo, pisamos la foto vieja
                    libro.PortadaLocalUrl = await _imagenService.GuardarImagenSubida(dto.ArchivoPortada, libro.Id);
                }
                else if (!string.IsNullOrWhiteSpace(dto.PortadaUrl) && string.IsNullOrEmpty(libro.PortadaLocalUrl))
                {
                    // Si tildó la caja pero no subió archivo, descargamos de Google (solo si no la habíamos descargado antes)
                    libro.PortadaLocalUrl = await _imagenService.DescargarImagenDesdeUrl(dto.PortadaUrl, libro.Id);
                }
            }
            else
            {
                // Si destildó la caja, "olvidamos" la foto local y volvemos a usar el link de internet
                libro.PortadaLocalUrl = null; 
            }

            // 3. Actualizar Tags
            libro.Tags.Clear();
            foreach (var nombreTag in dto.Tags ?? new List<string>())
            {
                var tagLimpio = nombreTag.Trim();
                var tagExistente = await _context.Tags.FirstOrDefaultAsync(t => t.Nombre.ToLower() == tagLimpio.ToLower());
                
                if (tagExistente != null) libro.Tags.Add(tagExistente);
                else libro.Tags.Add(new Tag { Nombre = tagLimpio });
            }

            // 4. Actualizar Ejemplares
            var idsRecibidos = dto.Ejemplares.Where(e => e.Id.HasValue).Select(e => e.Id.Value).ToList();
            var ejemplaresABorrar = libro.Ejemplares.Where(e => !idsRecibidos.Contains(e.Id)).ToList();
            _context.Ejemplares.RemoveRange(ejemplaresABorrar);

            foreach (var ejDto in dto.Ejemplares)
            {
                if (ejDto.Id.HasValue) 
                {
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
        // GET: api/libros/inventario
        [HttpGet("inventario")]
        public async Task<ActionResult<IEnumerable<object>>> GetInventarioCompleto()
        {
            // Buscamos directamente desde los Ejemplares, incluimos su Libro y ordenamos
            var inventario = await _context.Ejemplares
                .Include(e => e.Libro)
                .OrderBy(e => e.NumeroInventario)
                .Select(e => new {
                    Id = e.Id,
                    NumeroInventario = e.NumeroInventario,
                    Observaciones = e.Observaciones,
                    Disponible = e.DisponibleParaPrestamo,
                    LibroId = e.Libro.Id,
                    Titulo = e.Libro.Titulo,
                    Autor = e.Libro.AutorPrincipal,
                    Clasificacion = e.Libro.Clasificacion,
                    Cutter = e.Libro.CodigoCutter
                })
                .ToListAsync();

            if (!inventario.Any()) return NotFound(new { mensaje = "El inventario está vacío." });

            return Ok(inventario);
        }


        // GET: api/libros/cutter/borges
        [HttpGet("cutter/{autor}")]
        public IActionResult CalcularCutter(string autor)
        {
            if (string.IsNullOrWhiteSpace(autor)) return BadRequest();

            try 
            {
                // 1. Leemos el archivo JSON físico
                var rutaJson = Path.Combine(Directory.GetCurrentDirectory(), "cutter.json");
                var json = System.IO.File.ReadAllText(rutaJson);
                var tablaCutter = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(json);
                
                // 2. Limpiamos el autor (si escriben "Borges, Jorge", nos quedamos con "borges")
                var apellido = autor.Split(',')[0].Trim().ToLower();
                string numeroAsignado = "111"; // Número base por defecto
                
                // 3. El algoritmo Cutter real: Buscar el prefijo más cercano alfabéticamente
                var tablaOrdenada = tablaCutter.OrderBy(x => x.Key.ToLower()).ToList();

                foreach (var item in tablaOrdenada)
                {
                    // Si la tabla dice "borge" y el autor es "borges", nos sirve.
                    if (string.Compare(item.Key.ToLower(), apellido) <= 0)
                        numeroAsignado = item.Value.ToString();
                    else
                        break; // Si ya nos pasamos alfabéticamente, cortamos la búsqueda
                }

                // 4. El formato final es: Primera Letra (Mayúscula) + El número encontrado
                var resultado = char.ToUpper(apellido[0]) + numeroAsignado;
                
                return Ok(new { cutter = resultado });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensaje = "Error leyendo cutter.json", error = ex.Message });
            }
        }

        // GET: api/libros/lookup/isbn/978...
        [HttpGet("lookup/isbn/{isbn}")]
        public async Task<ActionResult<LibroLookupDTO>> LookupPorIsbn(string isbn, [FromQuery] string proveedor = "todas")
        {
            if (string.IsNullOrWhiteSpace(isbn)) return BadRequest();

            try
            {
                LibroLookupDTO? resultado = null;

                // 1. Intento con Google Books
                if (proveedor == "google" || proveedor == "todas")
                {
                    resultado = await BuscarEnGoogleBooks(isbn);
                }

                // 2. Intento con Open Library (Si eligió OL, o si eligió "todas" y Google falló)
                if ((resultado == null && proveedor == "todas") || proveedor == "openlibrary")
                {
                    resultado = await BuscarEnOpenLibrary(isbn);
                }

                // 3. Acá podés agregar tu 3ra API en el futuro (ej: ISBNdb)
                // if ((resultado == null && proveedor == "todas") || proveedor == "isbndb") { ... }

                if (resultado == null)
                {
                    return NotFound(new { mensaje = "Libro no encontrado en las bases de datos seleccionadas." });
                }

                return Ok(resultado);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR FATAL] {ex.Message}");
                return StatusCode(500, new { mensaje = "Error interno al buscar ISBN.", error = ex.Message });
            }
        }

        // --- MÉTODOS PRIVADOS DE CADA API ---

        private async Task<LibroLookupDTO?> BuscarEnGoogleBooks(string isbn)
        {
            using var httpClient = new HttpClient();
            // Acá podés sumar tu &key=TU_API_KEY para evitar el error 429
            var url = $"https://www.googleapis.com/books/v1/volumes?q=isbn:{isbn}"; 
            var response = await httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode) return null;

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.GetProperty("totalItems").GetInt32() == 0) return null;

            var volumeInfo = root.GetProperty("items")[0].GetProperty("volumeInfo");
            var resultado = new LibroLookupDTO
            {
                Titulo = volumeInfo.TryGetProperty("title", out var t) ? t.GetString()! : "",
                Subtitulo = volumeInfo.TryGetProperty("subtitle", out var s) ? s.GetString()! : "",
                AutorPrincipal = volumeInfo.TryGetProperty("authors", out var a) && a.GetArrayLength() > 0 ? a[0].GetString()! : "",
                Editorial = volumeInfo.TryGetProperty("publisher", out var p) ? p.GetString()! : "",
                AnioPublicacion = volumeInfo.TryGetProperty("publishedDate", out var d) && d.GetString()!.Length >= 4 ? d.GetString()!.Substring(0, 4) : "",
                ReseniaSinopsis = volumeInfo.TryGetProperty("description", out var desc) ? desc.GetString()! : "",
                CantidadPaginas = volumeInfo.TryGetProperty("pageCount", out var pc) ? pc.GetInt32() : null,
            };

            if (volumeInfo.TryGetProperty("imageLinks", out var images))
                resultado.PortadaUrl = images.TryGetProperty("thumbnail", out var thumb) ? thumb.GetString()! : "";

            if (volumeInfo.TryGetProperty("categories", out var cats) && cats.ValueKind == JsonValueKind.Array)
                foreach (var cat in cats.EnumerateArray()) resultado.Categorias.Add(cat.GetString()!);

            return resultado;
        }

        private async Task<LibroLookupDTO?> BuscarEnOpenLibrary(string isbn)
        {
            using var httpClient = new HttpClient();
            var url = $"https://openlibrary.org/api/books?bibkeys=ISBN:{isbn}&format=json&jscmd=data";
            var response = await httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode) return null;

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            // OpenLibrary devuelve un objeto con una key dinámica "ISBN:1234..."
            if (!root.TryGetProperty($"ISBN:{isbn}", out var bookData)) return null;

            var resultado = new LibroLookupDTO
            {
                Titulo = bookData.TryGetProperty("title", out var t) ? t.GetString()! : "",
                CantidadPaginas = bookData.TryGetProperty("number_of_pages", out var pc) ? pc.GetInt32() : null,
                AnioPublicacion = bookData.TryGetProperty("publish_date", out var pd) && pd.GetString()!.Length >= 4 ? pd.GetString()!.Substring(0, 4) : ""
            };

            if (bookData.TryGetProperty("authors", out var authors) && authors.GetArrayLength() > 0)
                resultado.AutorPrincipal = authors[0].TryGetProperty("name", out var n) ? n.GetString()! : "";

            if (bookData.TryGetProperty("publishers", out var pubs) && pubs.GetArrayLength() > 0)
                resultado.Editorial = pubs[0].TryGetProperty("name", out var n) ? n.GetString()! : "";

            if (bookData.TryGetProperty("cover", out var cover))
                resultado.PortadaUrl = cover.TryGetProperty("medium", out var m) ? m.GetString()! : "";

            if (bookData.TryGetProperty("subjects", out var subjects) && subjects.ValueKind == JsonValueKind.Array)
                foreach (var sub in subjects.EnumerateArray()) 
                    resultado.Categorias.Add(sub.TryGetProperty("name", out var sn) ? sn.GetString()! : "");

            return resultado;
        }

        // GET: api/libros/autores/buscar?q=bor
        [HttpGet("autores/buscar")]
        public async Task<ActionResult<IEnumerable<string>>> BuscarAutores([FromQuery] string q)
        {
            // Solo buscamos si el usuario tipeó al menos 2 letras
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2) return Ok(new List<string>());
            
            var autores = await _context.Libros
                .Where(l => l.AutorPrincipal.ToLower().Contains(q.ToLower()))
                .Select(l => l.AutorPrincipal)
                .Distinct() // Para no devolver 20 veces a "Borges" si tiene 20 libros
                .Take(10)   // Solo los primeros 10 para que el menú no sea infinito
                .ToListAsync();
                
            return Ok(autores);
        }

        [HttpGet("tags/buscar")]
        public async Task<ActionResult<IEnumerable<string>>> BuscarTagsUnesco([FromQuery] string q)
        {
            // Pide 2 letras mínimo para no saturar la base de datos
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2) return Ok(new List<string>());
            
            // AHORA BUSCAMOS EN EL TESAURO OFICIAL, NO EN LOS TAGS SUCIOS
            var tags = await _context.TesauroUnesco
                .Where(t => t.Termino.ToLower().Contains(q.ToLower()))
                .OrderBy(t => t.Termino) // Orden alfabético para que sea más prolijo
                .Select(t => t.Termino)
                .Take(10)
                .ToListAsync();
                
            return Ok(tags);
        }

        

        // GET: api/libros/cutter-table
        [HttpGet("cutter-table")]
        public IActionResult GetTablaCutterCompleta()
        {
            try 
            {
                var rutaJson = Path.Combine(Directory.GetCurrentDirectory(), "cutter.json");
                if (!System.IO.File.Exists(rutaJson)) return NotFound(new { mensaje = "Archivo cutter.json no encontrado." });
                
                var json = System.IO.File.ReadAllText(rutaJson);
                // Devolvemos el JSON crudo, ASP.NET se encarga de serializarlo como objeto
                return Content(json, "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensaje = "Error leyendo cutter.json", error = ex.Message });
            }
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
        public string? ReseniaSinopsis { get; set; }
        public int? CantidadPaginas { get; set; }
        public string? PortadaUrl { get; set; }
        public bool UsarPortadaLocal { get; set; }
        public IFormFile? ArchivoPortada { get; set; }
        
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
        public string? ReseniaSinopsis { get; set; }
        public int? CantidadPaginas { get; set; }
        public string? PortadaUrl { get; set; }
        public bool UsarPortadaLocal { get; set; }
        public IFormFile? ArchivoPortada { get; set; }
        
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

    // DTO simple para devolver datos encontrados en internet
    public class LibroLookupDTO
    {
        public string Titulo { get; set; } = "";
        public string Subtitulo { get; set; } = "";
        public string AutorPrincipal { get; set; } = "";
        public string Editorial { get; set; } = "";
        public string AnioPublicacion { get; set; } = "";

        // --- NUEVOS CAMPOS ---
        public string ReseniaSinopsis { get; set; } = "";
        public int? CantidadPaginas { get; set; }
        public string PortadaUrl { get; set; } = "";
        public List<string> Categorias { get; set; } = new List<string>();
    }

    
}