using Microsoft.EntityFrameworkCore;
using Backend.Models; // Asegurate de que el namespace coincida con el tuyo

namespace Backend.Data
{
    public class BibliotecaContext : DbContext
    {
        // El constructor que recibe la configuración (como la cadena de conexión)
        public BibliotecaContext(DbContextOptions<BibliotecaContext> options) : base(options)
        {
        }

        // Estas propiedades DbSet son las que se van a convertir en las tablas de PostgreSQL
        public DbSet<Libro> Libros { get; set; }
        public DbSet<Ejemplar> Ejemplares { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Prestamo> Prestamos { get; set; }
        public DbSet<Material> Materiales { get; set; }
        public DbSet<PrestamoMaterial> PrestamosMateriales { get; set; }
        public DbSet<TerminoUnesco> TesauroUnesco { get; set; }

        // Opcional: Acá podés agregar reglas estrictas de creación si querés
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
           
        }
    }
}