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

        [HttpGet("{id}")]
        public async Task<ActionResult<Material>> GetMaterial(int id)
        {
            var material = await _context.Materiales.FindAsync(id);

            if (material == null)
            {
                return NotFound(new { mensaje = "Material no encontrado." });
            }

            return Ok(material);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult> PutMaterial(int id, Material materialActualizado)
        {
            if (id != materialActualizado.Id) 
                return BadRequest(new { mensaje = "El ID no coincide." });

            var materialExistente = await _context.Materiales.FindAsync(id);
            if (materialExistente == null) 
                return NotFound(new { mensaje = "Material no encontrado." });

            // Magia de Stock: Si cambian el Total, ajustamos el Disponible proporcionalmente
            int diferenciaStock = materialActualizado.CantidadTotal - materialExistente.CantidadTotal;

            materialExistente.Nombre = materialActualizado.Nombre;
            materialExistente.Marca = materialActualizado.Marca;
            materialExistente.Modelo = materialActualizado.Modelo;
            materialExistente.NumeroSerie = materialActualizado.NumeroSerie;
            materialExistente.UbicacionFisica = materialActualizado.UbicacionFisica;
            materialExistente.Observaciones = materialActualizado.Observaciones;
            materialExistente.Habilitado = materialActualizado.Habilitado;
            
            materialExistente.CantidadTotal = materialActualizado.CantidadTotal;
            materialExistente.CantidadDisponible += diferenciaStock;

            // Seguro por si hacen lío
            if (materialExistente.CantidadDisponible < 0) 
                materialExistente.CantidadDisponible = 0;

            await _context.SaveChangesAsync();
            return Ok(materialExistente);
        }
    }
}