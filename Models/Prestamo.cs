namespace Backend.Models;
public class Prestamo
{
    public int Id { get; set; }
    
    // AHORA APUNTA AL EJEMPLAR FÍSICO
    public int EjemplarId { get; set; }
    public Ejemplar Ejemplar { get; set; } = null!;
    
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    
    public DateTime FechaSalida { get; set; }
    public DateTime FechaVencimiento { get; set; } 
    public DateTime? FechaDevolucionReal { get; set; } 
    public EstadoPrestamo Estado { get; set; } 
}


public enum EstadoPrestamo
{
    Activo,      // El libro está en posesión del usuario y en fecha
    Vencido,     // Pasó la FechaVencimiento y no se devolvió
    Devuelto     // El ciclo se cerró correctamente
}