namespace Backend.Models;
using System.Text.Json.Serialization;
public class Usuario
{
    public int Id { get; set; }
    public string Dni { get; set; } // Ideal para búsquedas rápidas o carnet
    public string Nombre { get; set; }
    public string Apellido { get; set; }
    public string? Telefono { get; set; }
    
    public RolUsuario Rol { get; set; } // Enum: Alumno, Profesor, Administrador
    
    // Datos específicos de la escuela (Pueden ser nulos si es Profesor)
    // public string Anio { get; set; } // Ej: "3ro"
    // public string Division { get; set; } // Ej: "B"

    public int? GrupoId { get; set; }
    public Grupo? Grupo { get; set; }
    
    // Control de estado
    public bool PuedePedirPrestado { get; set; } = true; // Se pone en false si debe libros o tiene sanción
    
    // Relación
    [JsonIgnore] // Para evitar ciclos infinitos al serializar a JSON
    public ICollection<Prestamo> Prestamos { get; set; } = new List<Prestamo>();

    [JsonIgnore]
    public ICollection<PrestamoMaterial> PrestamosMateriales { get; set; } = new List<PrestamoMaterial>();
}

public enum RolUsuario
{
    Alumno,
    Profesor,
    Administrador
}