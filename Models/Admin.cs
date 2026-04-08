namespace Backend.Models
{
    public class Admin
    {
        public int Id { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;
        
        // NUNCA guardamos la contraseña real, solo su "Hash" o huella digital
        public string PasswordHash { get; set; } = string.Empty; 
    }
}