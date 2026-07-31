using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace barberia_turnos_mvc.Migrations
{
    /// <inheritdoc />
    public partial class VincularClienteConUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplicationUserId",
                table: "Clientes",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_ApplicationUserId",
                table: "Clientes",
                column: "ApplicationUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Clientes_AspNetUsers_ApplicationUserId",
                table: "Clientes",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Clientes_AspNetUsers_ApplicationUserId",
                table: "Clientes");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_ApplicationUserId",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "Clientes");
        }
    }
}
