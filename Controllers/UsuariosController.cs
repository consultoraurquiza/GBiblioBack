using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsuariosController : ControllerBase
    {
        private readonly BibliotecaContext _context;

        public UsuariosController(BibliotecaContext context)
        {
            _context = context;
        }

        // 1. GET: Para la tabla principal
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Usuario>>> GetUsuarios([FromQuery] string? busqueda, [FromQuery] string grupoFiltro = "todos")
        {
            var query = _context.Usuarios.Include(u => u.Grupo).AsQueryable();

            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                var b = busqueda.ToLower();
                query = query.Where(u =>
                    u.Dni.ToLower().Contains(b) ||
                    u.Nombre.ToLower().Contains(b) ||
                    u.Apellido.ToLower().Contains(b));
            }

            if (grupoFiltro != "todos")
            {
                if (grupoFiltro == "alumnos") query = query.Where(u => u.Rol == RolUsuario.Alumno);
                else if (grupoFiltro == "docentes") query = query.Where(u => u.Rol == RolUsuario.Profesor);
                else if (grupoFiltro == "admin") query = query.Where(u => u.Rol == RolUsuario.Administrador);
            }

            var usuarios = await query.OrderBy(u => u.Apellido).ThenBy(u => u.Nombre).ToListAsync();
            return Ok(usuarios);
        }

        // 2. GET: Buscador rápido para el Autocompletado del Mostrador
        [HttpGet("buscar")]
        public async Task<ActionResult> BuscarUsuariosParaPrestamo([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Ok(new List<object>());

            var busqueda = q.ToLower();

            var usuarios = await _context.Usuarios
                .Include(u => u.Grupo) // 👈 Agregamos el Include para poder leer el nombre del grupo
                .Where(u => u.Dni.Contains(busqueda) || u.Nombre.ToLower().Contains(busqueda) || u.Apellido.ToLower().Contains(busqueda))
                .Select(u => new
                {
                    id = u.Id,
                    nombre = u.Nombre,
                    apellido = u.Apellido,
                    dni = u.Dni,
                    // 👈 Usamos el Grupo en lugar de Anio/Division
                    curso = u.Rol == RolUsuario.Alumno ? (u.Grupo != null ? u.Grupo.Nombre : "Sin asignar") : "Docente/Admin"
                })
                .Take(10)
                .ToListAsync();

            return Ok(usuarios);
        }

        // GET: Traer alumnos específicos de un grupo para el migrador
        // GET: Traer alumnos específicos de un grupo para el migrador
        [HttpGet("por-grupo/{grupoId}")]
        public async Task<ActionResult> GetUsuariosPorGrupo(int grupoId)
        {
            var usuarios = await _context.Usuarios
                .Where(u => u.GrupoId == grupoId && u.Rol == RolUsuario.Alumno)
                .OrderBy(u => u.Apellido).ThenBy(u => u.Nombre)
                // 👇 MAGIA ACÁ: Usamos Select para aplanar el JSON y evitar bucles
                .Select(u => new 
                {
                    id = u.Id,
                    nombre = u.Nombre,
                    apellido = u.Apellido,
                    dni = u.Dni
                })
                .ToListAsync();

            return Ok(usuarios);
        }

        // 3. POST: Crear nuevo usuario
        [HttpPost]
        public async Task<ActionResult<Usuario>> CrearUsuario(Usuario usuario)
        {
            if (await _context.Usuarios.AnyAsync(u => u.Dni == usuario.Dni))
                return BadRequest(new { mensaje = "Ya existe un usuario registrado con ese DNI." });

            // Si es profe o admin, forzamos a nulo el grupo
            if (usuario.Rol != RolUsuario.Alumno)
            {
                usuario.GrupoId = null; 
            }

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            return Ok(usuario);
        }

        // 4. PUT: Editar usuario existente
        [HttpPut("{id}")]
        public async Task<IActionResult> ActualizarUsuario(int id, Usuario usuarioActualizado)
        {
            if (id != usuarioActualizado.Id)
                return BadRequest(new { mensaje = "El ID del usuario no coincide." });

            var usuarioBd = await _context.Usuarios.FindAsync(id);
            if (usuarioBd == null)
                return NotFound(new { mensaje = "Usuario no encontrado." });

            if (usuarioBd.Dni != usuarioActualizado.Dni && await _context.Usuarios.AnyAsync(u => u.Dni == usuarioActualizado.Dni))
                return BadRequest(new { mensaje = "El DNI ingresado ya pertenece a otro usuario." });

            usuarioBd.Dni = usuarioActualizado.Dni;
            usuarioBd.Nombre = usuarioActualizado.Nombre;
            usuarioBd.Apellido = usuarioActualizado.Apellido;
            usuarioBd.Telefono = usuarioActualizado.Telefono;
            usuarioBd.Rol = usuarioActualizado.Rol;
            usuarioBd.PuedePedirPrestado = usuarioActualizado.PuedePedirPrestado;
            usuarioBd.GrupoId = usuarioActualizado.GrupoId;

            // 👈 LÓGICA CORREGIDA: Si NO es alumno, le borramos el grupo.
            if (usuarioActualizado.Rol != RolUsuario.Alumno)
            {
                usuarioBd.GrupoId = null;
            }

            await _context.SaveChangesAsync();

            return Ok(usuarioBd);
        }
    }
}