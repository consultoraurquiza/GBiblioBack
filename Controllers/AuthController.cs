using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Backend.Data; // Tu namespace del Contexto
using Backend.Models;
using Backend.DTOs;
using BCrypt.Net; // El paquete para contraseñas

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly BibliotecaContext _context;
        private readonly IConfiguration _config;

        public AuthController(BibliotecaContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // 1. Buscamos al administrador en la base de datos
            var admin = await _context.Administradores
                .FirstOrDefaultAsync(a => a.NombreUsuario == request.NombreUsuario);

            // 2. Verificamos que exista y que la contraseña coincida con el Hash
            if (admin == null || !BCrypt.Net.BCrypt.Verify(request.Password, admin.PasswordHash))
            {
                return Unauthorized(new { mensaje = "Usuario o contraseña incorrectos." });
            }

            // 3. Si todo está bien, generamos el Token JWT
            string token = GenerarJwtToken(admin);

            return Ok(new { token });
        }

        private string GenerarJwtToken(Admin admin)
        {
            // Leemos la clave secreta desde el appsettings.json
            var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("Falta la clave JWT en appsettings.");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Estos son los datos que viajan "dentro" del token
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
                new Claim(ClaimTypes.Name, admin.NombreUsuario),
                new Claim(ClaimTypes.Role, "Administrador") // Le clavamos el rol acá
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8), // El token dura 8 horas (la jornada escolar)
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}