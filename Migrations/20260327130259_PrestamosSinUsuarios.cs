using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class PrestamosSinUsuarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PrestamosMateriales_Usuarios_UsuarioId",
                table: "PrestamosMateriales");

            migrationBuilder.AlterColumn<int>(
                name: "UsuarioId",
                table: "PrestamosMateriales",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "NombreSolicitante",
                table: "PrestamosMateriales",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_PrestamosMateriales_Usuarios_UsuarioId",
                table: "PrestamosMateriales",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PrestamosMateriales_Usuarios_UsuarioId",
                table: "PrestamosMateriales");

            migrationBuilder.DropColumn(
                name: "NombreSolicitante",
                table: "PrestamosMateriales");

            migrationBuilder.AlterColumn<int>(
                name: "UsuarioId",
                table: "PrestamosMateriales",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PrestamosMateriales_Usuarios_UsuarioId",
                table: "PrestamosMateriales",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
