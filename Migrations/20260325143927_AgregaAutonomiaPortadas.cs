using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class AgregaAutonomiaPortadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PortadaLocalUrl",
                table: "Libros",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UsarPortadaLocal",
                table: "Libros",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PortadaLocalUrl",
                table: "Libros");

            migrationBuilder.DropColumn(
                name: "UsarPortadaLocal",
                table: "Libros");
        }
    }
}
