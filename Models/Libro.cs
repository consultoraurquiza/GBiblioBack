namespace Backend.Models;
using System.Text.Json.Serialization;
public class Libro
{
    public int Id { get; set; }
    public string Isbn { get; set; } // Útil si en el futuro usan lector de código de barras
    public string Titulo { get; set; }
    public string Autor { get; set; }
    public string Editorial { get; set; }
    public int AnioPublicacion { get; set; }
    
    // Clasificación escolar
    public string Materia { get; set; } // Ej: "Historia", "Matemáticas"
    public string UbicacionFisica { get; set; } // Ej: "Estante 3 - Pasillo A"

    // Control de inventario
    public int CantidadTotal { get; set; }
    public int CantidadDisponible { get; set; } 
    
    // Relación
    [JsonIgnore] // Para evitar ciclos infinitos al serializar a JSON
    public ICollection<Prestamo> Prestamos { get; set; } = new List<Prestamo>();
}