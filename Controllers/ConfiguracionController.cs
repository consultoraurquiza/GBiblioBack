using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data; // Ajustá a tu namespace
using Backend.Models;

[Route("api/[controller]")]
[ApiController]
public class ConfiguracionController : ControllerBase
{
    private readonly BibliotecaContext _context;

    public ConfiguracionController(BibliotecaContext context)
    {
        _context = context;
    }

    // 1. OBTENER CONFIGURACIÓN
    [HttpGet]
    public async Task<ActionResult<Configuracion>> GetConfiguracion()
    {
        var config = await _context.Configuracion.FirstOrDefaultAsync(c => c.Id == 1);

        if (config == null)
        {
            // Si por alguna razón no existe (primera vez), la creamos por defecto
            config = new Configuracion { Id = 1, NombreEscuela = "Biblioteca EETP 464", TemaId = "oxford" };
            _context.Configuracion.Add(config);
            await _context.SaveChangesAsync();
        }

        return config;
    }

    // 2. ACTUALIZAR CONFIGURACIÓN (Personalización y Ajustes)
    [HttpPut]
    public async Task<IActionResult> UpdateConfiguracion(Configuracion configActualizada)
    {
        // Siempre forzamos el ID 1 para que no se creen filas nuevas
        var configExistente = await _context.Configuracion.FirstOrDefaultAsync(c => c.Id == 1);

        if (configExistente == null) return NotFound();

        // Actualizamos solo los campos que permitimos desde el Front
        configExistente.NombreEscuela = configActualizada.NombreEscuela;
        configExistente.TemaId = configActualizada.TemaId;
        configExistente.LogoUrl = configActualizada.LogoUrl;
        configExistente.DiasPrestamo = configActualizada.DiasPrestamo;
        configExistente.MaxLibrosPorPersona = configActualizada.MaxLibrosPorPersona;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return StatusCode(500, "Error al guardar los cambios.");
        }

        return NoContent();
    }
}