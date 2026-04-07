public class Configuracion
{
    public int Id { get; set; } = 1;
    public string NombreEscuela { get; set; } = "Biblioteca Escolar";
    public string TemaId { get; set; } = "oxford"; // oxford, botanico, obsidian
    public string? LogoUrl { get; set; }
    public int DiasPrestamo { get; set; } = 7;
    public int MaxLibrosPorPersona { get; set; } = 3;
}