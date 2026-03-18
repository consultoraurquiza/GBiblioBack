using System.Text.Json.Serialization;

namespace Backend.Models;

public class Tag
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty; // Ej: "Ficción", "Historia Argentina"

    [JsonIgnore]
    public ICollection<Libro> Libros { get; set; } = new List<Libro>();
}