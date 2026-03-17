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

        // GET: api/usuarios (Trae toda la lista)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Usuario>>> GetUsuarios()
        {
            return await _context.Usuarios.ToListAsync();
        }

        // GET: api/usuarios/buscar/12345678
        // ¡Este es el buscador estrella para el bibliotecario!
        [HttpGet("buscar/{dni}")]
        public async Task<ActionResult<Usuario>> BuscarPorDni(string dni)
        {
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Dni == dni);

            if (usuario == null)
            {
                // Si devuelve 404, en el frontend mostramos la ventanita de "Nuevo Usuario"
                return NotFound(new { mensaje = "El usuario no existe. Debe registrarse." });
            }

            return Ok(usuario);
        }

        // POST: api/usuarios (Registra al alumno en el momento)
        [HttpPost]
        public async Task<ActionResult<Usuario>> PostUsuario(Usuario usuario)
        {
            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetUsuarios), new { id = usuario.Id }, usuario);
        }
        // PUT: api/usuarios/5 (Edita un usuario existente)
        [HttpPut("{id}")]
        public async Task<IActionResult> EditarUsuario(int id, Usuario usuarioActualizado)
        {
            if (id != usuarioActualizado.Id) 
                return BadRequest(new { mensaje = "El ID no coincide." });

            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null) 
                return NotFound(new { mensaje = "Usuario no encontrado." });

            // Actualizamos solo los campos permitidos
            usuario.Dni = usuarioActualizado.Dni;
            usuario.Nombre = usuarioActualizado.Nombre;
            usuario.Apellido = usuarioActualizado.Apellido;
            usuario.Telefono = usuarioActualizado.Telefono;
            usuario.Rol = usuarioActualizado.Rol;
            usuario.Anio = usuarioActualizado.Anio;
            usuario.Division = usuarioActualizado.Division;
            usuario.PuedePedirPrestado = usuarioActualizado.PuedePedirPrestado;

            await _context.SaveChangesAsync();
            
            return Ok(usuario);
        }

        // También necesitamos un GET por ID normal para que el formulario de Next.js pueda cargar los datos
        [HttpGet("{id}")]
        public async Task<ActionResult<Usuario>> GetUsuarioPorId(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null) return NotFound();
            return Ok(usuario);
        }
    }
}