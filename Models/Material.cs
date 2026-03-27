using System.Text.Json.Serialization;

namespace Backend.Models;

public class Material
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty; // Ej: Proyector, Calculadora, Mapa Político
    public string? Marca { get; set; }
    public string? Modelo { get; set; }
    public string? NumeroSerie { get; set; } // Muy útil para saber QUÉ proyector se llevó
    
    public string? UbicacionFisica { get; set; } // Ej: Armario 2 - Dirección
    
    // Control de inventario
    public int CantidadTotal { get; set; }
    public int CantidadDisponible { get; set; }

    public string? Observaciones { get; set; } // Ej: "Falta cable de poder", "Lámpara quemada"
    public bool Habilitado { get; set; } = true; // true = Operativo, false = Roto/En Reparación

    [JsonIgnore]
    public ICollection<PrestamoMaterial> PrestamosMateriales { get; set; } = new List<PrestamoMaterial>();
}