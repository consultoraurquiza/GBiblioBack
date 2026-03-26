using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using Backend.Models; // ¡Cambiá esto por tu namespace!

namespace Backend.Data
{
    public static class DbSeeder
    {
        public static async Task InicializarTesauro(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BibliotecaContext>();

            if (await context.TesauroUnesco.AnyAsync()) return;

            // Buscamos el archivo de la UNESCO (asegurate de que el nombre sea exacto)
            var rutaArchivo = Path.Combine(AppContext.BaseDirectory, "Data", "tesauro.ttl");
            
            if (!File.Exists(rutaArchivo))
            {
                Console.WriteLine("⚠️ [SEEDER] No se encontró el archivo del tesauro en: " + rutaArchivo);
                return;
            }

            Console.WriteLine("⏳ [SEEDER] Analizando archivo Turtle de la UNESCO. Extrayendo términos en español...");

            // Forzamos la lectura en UTF-8 para arreglar el "CatalogaciÃ³n"
            var lineas = await File.ReadAllLinesAsync(rutaArchivo, System.Text.Encoding.UTF8);
            
            // Usamos un HashSet para no guardar palabras repetidas
            var terminosUnicos = new HashSet<string>();
            
            // Esta magia busca cualquier texto entre comillas que termine en @es
            // Ej: skos:prefLabel "Catalogación"@es;
            var regex = new Regex(@"Label\s+""([^""]+)""@es");

            foreach (var linea in lineas)
            {
                if (linea.Contains("@es"))
                {
                    var match = regex.Match(linea);
                    if (match.Success)
                    {
                        // match.Groups[1] contiene la palabra limpia sin las comillas ni el @es
                        var terminoLimpio = match.Groups[1].Value.Trim();
                        terminosUnicos.Add(terminoLimpio);
                    }
                }
            }

            // Convertimos las palabras a objetos para la Base de Datos
            var terminosParaGuardar = terminosUnicos
                .Select(t => new TerminoUnesco { Termino = t })
                .ToList();

            if (terminosParaGuardar.Any())
            {
                await context.TesauroUnesco.AddRangeAsync(terminosParaGuardar);
                await context.SaveChangesAsync();
                Console.WriteLine($"✅ [SEEDER] ¡Éxito brutal! Se inyectaron {terminosParaGuardar.Count} términos normalizados en la base de datos.");
            }
        }
    }
}