using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class DatosExpandidosLibro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CantidadPaginas",
                table: "Libros",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PortadaUrl",
                table: "Libros",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReseniaSinopsis",
                table: "Libros",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CantidadPaginas",
                table: "Libros");

            migrationBuilder.DropColumn(
                name: "PortadaUrl",
                table: "Libros");

            migrationBuilder.DropColumn(
                name: "ReseniaSinopsis",
                table: "Libros");
        }
    }
}
