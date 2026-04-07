using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BackupController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly IConfiguration _config;

        // Inyectamos la configuración para leer el appsettings.json
        public BackupController(IConfiguration configuration)
{
    _config = configuration;
    // El "??" le dice a C#: Si es nulo, lanzá un error claro y detenete.
    _connectionString = configuration.GetConnectionString("DefaultConnection") 
        ?? throw new InvalidOperationException("No se encontró 'DefaultConnection' en el appsettings.json.");
}

        // GET: api/backup/exportar
        [HttpGet("exportar")]
        public async Task<IActionResult> ExportarBackup()
        {
            try
            {
                string tempRuta = Path.Combine(Path.GetTempPath(), $"backup_{Guid.NewGuid()}.bak");

                // Ejecutamos pg_dump
                bool exito = EjecutarPgDump(tempRuta);

                if (!exito || !System.IO.File.Exists(tempRuta))
                    return StatusCode(500, new { mensaje = "No se pudo generar el archivo de respaldo con pg_dump." });

                byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(tempRuta);
                System.IO.File.Delete(tempRuta);

                string fecha = DateTime.Now.ToString("yyyy-MM-dd");
                return File(fileBytes, "application/octet-stream", $"backup_biblioteca_{fecha}.bak");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensaje = "Error al exportar: " + ex.Message });
            }
        }

        // POST: api/backup/restaurar
        [HttpPost("restaurar")]
        public async Task<IActionResult> RestaurarBackup([FromForm] IFormFile archivoBackup)
        {
            if (archivoBackup == null || archivoBackup.Length == 0)
            {
                return BadRequest(new { mensaje = "No se envió ningún archivo válido." });
            }

            string tempRuta = Path.Combine(Path.GetTempPath(), $"restore_{Guid.NewGuid()}.bak");

            try
            {
                // Guardamos el archivo subido en el servidor
                using (var stream = new FileStream(tempRuta, FileMode.Create))
                {
                    await archivoBackup.CopyToAsync(stream);
                }

                // Antes de restaurar, desconectamos a los demás usuarios para que no bloqueen la base
                DesconectarUsuarios();

                // Ejecutamos pg_restore
                bool exito = EjecutarPgRestore(tempRuta);

                if (!exito)
                {
                    return StatusCode(500, new { mensaje = "El archivo se recibió, pero pg_restore falló al intentar aplicarlo." });
                }

                return Ok(new { mensaje = "Base de datos restaurada correctamente." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensaje = "Error crítico al restaurar: " + ex.Message });
            }
            finally
            {
                if (System.IO.File.Exists(tempRuta))
                {
                    System.IO.File.Delete(tempRuta);
                }
            }
        }

        // ========================================================================
        // MÉTODOS PRIVADOS PARA LLAMAR A LA TERMINAL DE LINUX/WINDOWS
        // ========================================================================

        private bool EjecutarPgDump(string rutaDestino)
{
    var builder = new NpgsqlConnectionStringBuilder(_connectionString);
    string argumentos = $"-h {builder.Host} -p {builder.Port} -U {builder.Username} -F c -b -f \"{rutaDestino}\" {builder.Database}";

    string comando = ObtenerRutaHerramienta("PgDumpPath", "WindowsDumpPath", "LinuxDumpPath");
    return EjecutarProceso(comando, argumentos, builder.Password);
}

private bool EjecutarPgRestore(string rutaOrigen)
{
    var builder = new NpgsqlConnectionStringBuilder(_connectionString);
    string argumentos = $"-h {builder.Host} -p {builder.Port} -U {builder.Username} -d {builder.Database} -c -1 \"{rutaOrigen}\"";

    string comando = ObtenerRutaHerramienta("PgRestorePath", "WindowsRestorePath", "LinuxRestorePath");
    return EjecutarProceso(comando, argumentos, builder.Password);
}

// ESTE ES EL MÉTODO INTELIGENTE QUE DETECTA EL OS
private string ObtenerRutaHerramienta(string nombreGenerico, string configWindows, string configLinux)
{
    string ruta = "";

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        ruta = _config[$"PostgresTools:{configWindows}"];
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        ruta = _config[$"PostgresTools:{configLinux}"];
    }

    // Si la ruta está vacía en el appsettings, intentamos usar el comando global por defecto
    if (string.IsNullOrEmpty(ruta))
    {
        return nombreGenerico.Replace("Path", "").ToLower(); // Retorna "pg_dump" o "pg_restore" a secas
    }

    return ruta;
}
        private bool EjecutarProceso(string comando, string argumentos, string password)
{
    var processInfo = new ProcessStartInfo
    {
        FileName = comando,
        Arguments = argumentos,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    if (!string.IsNullOrEmpty(password))
    {
        processInfo.EnvironmentVariables["PGPASSWORD"] = password;
    }

    try
    {
        using (var process = Process.Start(processInfo))
        {
            if (process == null) return false;
            
            process.WaitForExit();
            
            if (process.ExitCode != 0)
            {
                // CAPTURAMOS EL ERROR REAL DE LA TERMINAL DE LINUX/WINDOWS
                string error = process.StandardError.ReadToEnd();
                throw new Exception($"La consola devolvió el siguiente error al ejecutar {comando}: {error}");
            }
            return true;
        }
    }
    catch (System.ComponentModel.Win32Exception)
    {
        // Este es el error típico si el sistema no encuentra "pg_dump" instalado o en las variables de entorno
        throw new Exception($"No se encontró el comando '{comando}'. Asegurate de que PostgreSQL esté instalado y agregado al PATH del sistema.");
    }
}

        private void DesconectarUsuarios()
        {
            try
            {
                var builder = new NpgsqlConnectionStringBuilder(_connectionString);
                string dbName = builder.Database;

                // Nos conectamos temporalmente a la base "postgres" (la base maestra)
                builder.Database = "postgres"; 
                
                using (var conn = new NpgsqlConnection(builder.ConnectionString))
                {
                    conn.Open();
                    // Este comando "patea" a todos los usuarios conectados a tu base de datos
                    string query = @"
                        SELECT pg_terminate_backend(pg_stat_activity.pid)
                        FROM pg_stat_activity
                        WHERE pg_stat_activity.datname = @dbName
                          AND pid <> pg_backend_pid();";

                    using (var cmd = new NpgsqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("dbName", dbName);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("No se pudieron desconectar las sesiones previas: " + ex.Message);
            }
        }
    }
}