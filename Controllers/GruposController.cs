using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GruposController : ControllerBase
    {
        private readonly BibliotecaContext _context;

        public GruposController(BibliotecaContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetGrupos([FromQuery] string? busqueda)
        {
            var query = _context.Grupos
                .Include(g => g.Alumnos) // Incluimos los alumnos para contarlos
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                query = query.Where(g => g.Nombre.ToLower().Contains(busqueda.ToLower()));
            }

            // Devolvemos un objeto anónimo con la cantidad de alumnos ya calculada
            var grupos = await query
                .OrderBy(g => g.Turno).ThenBy(g => g.Nombre)
                .Select(g => new
                {
                    g.Id,
                    g.Nombre,
                    g.Turno,
                    CantidadAlumnos = g.Alumnos.Count
                })
                .ToListAsync();

            return Ok(grupos);
        }

        [HttpPost]
        public async Task<ActionResult<Grupo>> CrearGrupo(Grupo grupo)
        {
            if (await _context.Grupos.AnyAsync(g => g.Nombre.ToLower() == grupo.Nombre.ToLower() && g.Turno == grupo.Turno))
                return BadRequest(new { mensaje = "Ya existe un grupo con ese nombre en ese turno." });

            _context.Grupos.Add(grupo);
            await _context.SaveChangesAsync();
            return Ok(grupo);
        }

        // POST: Mover múltiples alumnos a un nuevo grupo
        [HttpPost("migrar")]
        public async Task<IActionResult> MigrarAlumnos([FromBody] MigracionRequest request)
        {
            if (request.UsuarioIds == null || !request.UsuarioIds.Any())
                return BadRequest(new { mensaje = "Debe seleccionar al menos un alumno para migrar." });

            var grupoDestino = await _context.Grupos.FindAsync(request.NuevoGrupoId);
            if (grupoDestino == null)
                return NotFound(new { mensaje = "El grupo de destino no existe." });

            // Traemos todos los alumnos seleccionados
            var alumnos = await _context.Usuarios
                                        .Where(u => request.UsuarioIds.Contains(u.Id))
                                        .ToListAsync();

            // Les cambiamos el ID del grupo a todos
            foreach (var alumno in alumnos)
            {
                alumno.GrupoId = request.NuevoGrupoId;
            }

            await _context.SaveChangesAsync();
            return Ok(new { mensaje = $"¡Éxito! Se migraron {alumnos.Count} alumnos al grupo {grupoDestino.Nombre}." });
        }




        [HttpPut("{id}")]
        public async Task<IActionResult> ActualizarGrupo(int id, Grupo grupoActualizado)
        {
            if (id != grupoActualizado.Id) return BadRequest();

            var grupoBd = await _context.Grupos.FindAsync(id);
            if (grupoBd == null) return NotFound();

            grupoBd.Nombre = grupoActualizado.Nombre;
            grupoBd.Turno = grupoActualizado.Turno;

            await _context.SaveChangesAsync();
            return Ok(grupoBd);
        }
        // Clase auxiliar para recibir los datos de React
        public class MigracionRequest
        {
            public List<int> UsuarioIds { get; set; } = new List<int>();
            public int NuevoGrupoId { get; set; }
        }
    }
}