using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class PrestamosManuales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Prestamos_Usuarios_UsuarioId",
                table: "Prestamos");

            migrationBuilder.AlterColumn<int>(
                name: "UsuarioId",
                table: "Prestamos",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "CursoOAula",
                table: "Prestamos",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NombreLector",
                table: "Prestamos",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_Prestamos_Usuarios_UsuarioId",
                table: "Prestamos",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Prestamos_Usuarios_UsuarioId",
                table: "Prestamos");

            migrationBuilder.DropColumn(
                name: "CursoOAula",
                table: "Prestamos");

            migrationBuilder.DropColumn(
                name: "NombreLector",
                table: "Prestamos");

            migrationBuilder.AlterColumn<int>(
                name: "UsuarioId",
                table: "Prestamos",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Prestamos_Usuarios_UsuarioId",
                table: "Prestamos",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
