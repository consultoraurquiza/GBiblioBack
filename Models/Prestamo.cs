namespace Backend.Models;
public class Prestamo
{
    public int Id { get; set; }
    
    // Claves foráneas
    public int LibroId { get; set; }
    public Libro Libro { get; set; }
    
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; }
    
    // Fechas importantes
    public DateTime FechaSalida { get; set; }
    public DateTime FechaVencimiento { get; set; } // Fecha límite para devolver
    public DateTime? FechaDevolucionReal { get; set; } // Nullable, porque al crear el préstamo aún no se devolvió
    
    public EstadoPrestamo Estado { get; set; } // Enum para saber rápido la situación
}

public enum EstadoPrestamo
{
    Activo,      // El libro está en posesión del usuario y en fecha
    Vencido,     // Pasó la FechaVencimiento y no se devolvió
    Devuelto     // El ciclo se cerró correctamente
}