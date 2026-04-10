using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;
using Backend.Data; 
using Backend.Models; 
using Microsoft.AspNetCore.Authorization;
using System.Text.RegularExpressions;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImportacionController : ControllerBase
    {
        private readonly BibliotecaContext _context;
        // 👇 ESTA ES LA CLAVE PARA FILTRAR TUS LIBROS:
        private const string CODIGO_ESCUELA = "EET464";

        public ImportacionController(BibliotecaContext context)
        {
            _context = context;
        }

        [HttpPost("importar-koha")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
        public async Task<IActionResult> ImportarDesdeKoha([FromForm] IFormFile archivoXml)
        {
            if (archivoXml == null || archivoXml.Length == 0) return BadRequest(new { mensaje = "Archivo vacío." });

            try
            {
                using var stream = archivoXml.OpenReadStream();
                var xml = XDocument.Load(stream);
                var records = xml.Descendants().Where(e => e.Name.LocalName == "record");

                int librosImportados = 0;
                int ejemplaresImportados = 0;
                
                // 1. Cargamos los tags existentes de tu biblioteca y el Tesauro de la UNESCO
                var tagsDb = await _context.Tags.ToListAsync();
                var tagsExistentes = tagsDb
                    .GroupBy(t => QuitarAcentos(t.Nombre.ToLower()))
                    .ToDictionary(
                        grupo => grupo.Key,
                        grupo => grupo.First()
                    );
                var terminosUnescoDb = await _context.TesauroUnesco.Select(t => t.Termino).ToListAsync();

                var diccionarioUnesco = terminosUnescoDb
                    .GroupBy(termino => QuitarAcentos(termino.ToLower())) // Agrupamos por las dudas haya duplicados
                    .ToDictionary(
                     grupo => grupo.Key,               // Key: sin acentos (ej: "matematicas")
                     grupo => grupo.First()            // Value: oficial (ej: "Matemáticas")
                    );

                foreach (var record in records)
                {
                    // 1. EXTRAER EJEMPLARES (Tag 952 en Koha) Y FILTRAR POR ESCUELA
                    var ejemplaresKoha = record.Descendants().Where(e => e.Name.LocalName == "datafield" && e.Attribute("tag")?.Value == "952");
                    var listaEjemplares = new List<Ejemplar>();
                    string? signaturaTopografica = null; 

                    foreach (var item in ejemplaresKoha)
                    {
                        var sucursal = ObtenerSubcampo(item, "a") ?? ObtenerSubcampo(item, "b");

                        if (sucursal != null && sucursal.Contains(CODIGO_ESCUELA))
                        {
                            var codigoBarras = ObtenerSubcampo(item, "p"); 
                            signaturaTopografica ??= ObtenerSubcampo(item, "o"); 

                            listaEjemplares.Add(new Ejemplar
                            {
                                NumeroInventario = string.IsNullOrWhiteSpace(codigoBarras) ? "S/N" : codigoBarras,
                                DisponibleParaPrestamo = true
                            });
                        }
                    }

                    // 🚨 SI NO HAY EJEMPLARES DE ESTA ESCUELA, IGNORAMOS EL LIBRO ENTERO 🚨
                    if (!listaEjemplares.Any()) continue;

                    // 2. EXTRAER DATOS BIBLIOGRÁFICOS
                    string titulo = (ObtenerSubcampoMarc(record, "245", "a") ?? "Sin título").TrimEnd('/', ' ', '.', ':');
                    string autor = (ObtenerSubcampoMarc(record, "100", "a") ?? "Anónimo").TrimEnd(',', ' ', '.');
                    string isbn = ObtenerSubcampoMarc(record, "020", "a") ?? string.Empty;
                    string sinopsis = ObtenerSubcampoMarc(record, "520", "a") ?? string.Empty;

                    string editorial = (ObtenerSubcampoMarc(record, "260", "b") ?? ObtenerSubcampoMarc(record, "264", "b") ?? string.Empty).TrimEnd(',', ' ', ';');
                    string anioRaw = ObtenerSubcampoMarc(record, "260", "c") ?? ObtenerSubcampoMarc(record, "264", "c") ?? string.Empty;
                    int? anioPublicacion = ExtraerNumero(anioRaw);

                    string paginasRaw = ObtenerSubcampoMarc(record, "300", "a") ?? string.Empty;
                    int? cantidadPaginas = ExtraerNumero(paginasRaw);

                    string cdu = ObtenerSubcampoMarc(record, "080", "a") ?? ObtenerSubcampoMarc(record, "082", "a") ?? string.Empty;
                    string cutter = string.Empty;

                    if (string.IsNullOrWhiteSpace(cdu) && !string.IsNullOrWhiteSpace(signaturaTopografica))
                    {
                        var partesSignatura = signaturaTopografica.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (partesSignatura.Length > 0) cdu = partesSignatura[0];
                        if (partesSignatura.Length > 1) cutter = partesSignatura[1];
                    }

                    // 2.5 EXTRAER TAGS / MATERIAS (Tags 650 y 653 en Koha)
                    var tagsKoha = record.Descendants().Where(e => e.Name.LocalName == "datafield" &&
                                             (e.Attribute("tag")?.Value == "650" || e.Attribute("tag")?.Value == "653"));
                    var listaTagsDelLibro = new List<Tag>();

                    foreach (var tagField in tagsKoha)
                    {
                        string? rawTag = ObtenerSubcampo(tagField, "a")?.TrimEnd('.', ',', ' ', '/');

                        if (!string.IsNullOrWhiteSpace(rawTag))
                        {
                            string tagNormalizado = QuitarAcentos(rawTag.ToLower());
                            string nombreFinalParaGuardar = rawTag;

                            // 1. ¿Coincide con el Tesauro de la UNESCO?
                            if (diccionarioUnesco.TryGetValue(tagNormalizado, out var terminoOficialUnesco))
                            {
                                nombreFinalParaGuardar = terminoOficialUnesco;
                            }

                            // 2. ¿Ya existe en nuestra base de datos general de Tags?
                            if (tagsExistentes.TryGetValue(QuitarAcentos(nombreFinalParaGuardar.ToLower()), out var tagGuardado))
                            {
                                if (!listaTagsDelLibro.Any(t => t.Id == tagGuardado.Id))
                                {
                                    listaTagsDelLibro.Add(tagGuardado);
                                }
                            }
                            else
                            {
                                // 3. Es un tag totalmente nuevo: lo creamos
                                var nuevoTag = new Tag { Nombre = nombreFinalParaGuardar };
                                tagsExistentes[QuitarAcentos(nombreFinalParaGuardar.ToLower())] = nuevoTag;
                                listaTagsDelLibro.Add(nuevoTag);
                            }
                        }
                    }

                    // 3. ARMAR EL LIBRO Y GUARDAR
                    var nuevoLibro = new Libro
                    {
                        Titulo = titulo,
                        AutorPrincipal = autor,
                        Isbn = isbn,
                        Editorial = editorial,
                        AnioPublicacion = string.IsNullOrEmpty(anioPublicacion?.ToString()) ? null : anioPublicacion.ToString(),
                        CantidadPaginas = cantidadPaginas,
                        Clasificacion = cdu,
                        CodigoCutter = cutter,
                        Ejemplares = listaEjemplares,
                        Tags = listaTagsDelLibro, // 👈 ¡Ahora sí se guardan las materias!
                        ReseniaSinopsis = sinopsis,
                    };

                    _context.Libros.Add(nuevoLibro);
                    librosImportados++;
                    ejemplaresImportados += listaEjemplares.Count;
                }

                await _context.SaveChangesAsync();

                return Ok(new { mensaje = $"¡Migración perfecta! Se importaron {librosImportados} libros con un total de {ejemplaresImportados} copias físicas (exclusivos de la EET 464)." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensaje = "Error al leer el XML: " + ex.Message });
            }
        }

        // ==========================================
        // FUNCIONES AUXILIARES
        // ==========================================
        private string? ObtenerSubcampoMarc(XElement record, string tag, string code)
        {
            var field = record.Descendants().FirstOrDefault(e => e.Name.LocalName == "datafield" && e.Attribute("tag")?.Value == tag);
            return field != null ? ObtenerSubcampo(field, code) : null;
        }

        private string? ObtenerSubcampo(XElement field, string code)
        {
            return field.Descendants().FirstOrDefault(e => e.Name.LocalName == "subfield" && e.Attribute("code")?.Value == code)?.Value;
        }

        private int? ExtraerNumero(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            var match = Regex.Match(texto, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int resultado)) return resultado;
            return null;
        }

        // 👈 ESTA FUNCIÓN FALTABA (Sirve para limpiar los acentos y comparar bien las palabras)
        private string QuitarAcentos(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return texto;
            
            var normalizedString = texto.Normalize(System.Text.NormalizationForm.FormD);
            var stringBuilder = new System.Text.StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }
    }
}