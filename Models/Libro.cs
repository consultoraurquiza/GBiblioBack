namespace Backend.Models;

public class Libro
{
    public int Id { get; set; }

    // --- Datos MARC 21 Simplificados ---
    public string? Isbn { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string? Subtitulo { get; set; }
    public string AutorPrincipal { get; set; } = string.Empty;
    public string? Editorial { get; set; }
    public string? AnioPublicacion { get; set; }
    
    // --- Signatura Topográfica (Para encontrarlo en el estante) ---
    public string? Clasificacion { get; set; } // Ej: 863 (Literatura) -> Sistema Dewey o CDU
    public string? CodigoCutter { get; set; }  // Ej: B732 (Borges)
    
    // Relación con los Tags (Muchos a Muchos)
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();

    // Relación con los Ejemplares Físicos (Uno a Muchos)
    public ICollection<Ejemplar> Ejemplares { get; set; } = new List<Ejemplar>();
}