namespace Backend.Models;

public class PrestamoMaterial
{
    public int Id { get; set; }
    
    // Claves foráneas
    public int MaterialId { get; set; }
    public Material Material { get; set; } = null!;
    
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    
    // Fechas
    public DateTime FechaSalida { get; set; }
    public DateTime FechaVencimiento { get; set; } 
    public DateTime? FechaDevolucionReal { get; set; } 
    
    public EstadoPrestamo Estado { get; set; } 
}