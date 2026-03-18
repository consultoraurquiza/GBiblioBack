using System.Text.Json.Serialization;

namespace Backend.Models;

public class Ejemplar
{
    public int Id { get; set; }
    
    // ESTE es el número único que te pidió. Suele ser el código de barras.
    public string NumeroInventario { get; set; } = string.Empty; 
    
    public string? Observaciones { get; set; } // Ej: "Faltan páginas 10 a 12"
    public bool DisponibleParaPrestamo { get; set; } = true;

    // Relación con el Libro abstracto
    public int LibroId { get; set; }
    
    
    public Libro Libro { get; set; } = null!;
}