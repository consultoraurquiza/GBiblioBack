using System.ComponentModel.DataAnnotations;

namespace Backend.Models; // Cambiá "TuProyecto" por el nombre real de tu namespace

    public class TerminoUnesco
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string Termino { get; set; } = string.Empty;
    }
