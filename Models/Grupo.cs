using System.Text.Json.Serialization;

namespace Backend.Models
{
    public class Grupo
    {
        public int Id { get; set; }
        public string Nombre { get; set; } // Ej: "1ero A"
        public string Turno { get; set; } = "Mañana"; // Ej: Mañana, Tarde, Vespertino

        // Relación: Un grupo tiene muchos alumnos
        [JsonIgnore]
        public ICollection<Usuario> Alumnos { get; set; } = new List<Usuario>();
    }
}