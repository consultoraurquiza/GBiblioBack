using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MaterialesController : ControllerBase
    {
        private readonly BibliotecaContext _context;

        public MaterialesController(BibliotecaContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Material>>> GetMateriales()
        {
            return await _context.Materiales.ToListAsync();
        }

        [HttpGet("buscar/{texto}")]
        public async Task<ActionResult<IEnumerable<Material>>> BuscarMaterial(string texto)
        {
            var busqueda = texto.ToLower();
            var materiales = await _context.Materiales
                .Where(m => m.Nombre.ToLower().Contains(busqueda) || 
                            (m.NumeroSerie != null && m.NumeroSerie.ToLower() == busqueda))
                .ToListAsync();

            if (!materiales.Any()) return NotFound(new { mensaje = "No se encontraron materiales." });
            return Ok(materiales);
        }

        [HttpPost]
        public async Task<ActionResult<Material>> PostMaterial(Material material)
        {
            _context.Materiales.Add(material);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetMateriales), new { id = material.Id }, material);
        }
    }
}