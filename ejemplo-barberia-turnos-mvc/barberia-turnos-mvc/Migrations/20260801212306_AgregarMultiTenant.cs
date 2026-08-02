using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace barberia_turnos_mvc.Migrations
{
    /// <inheritdoc />
    public partial class AgregarMultiTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BarberiaId",
                table: "Clientes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BarberiaId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_BarberiaId_Telefono",
                table: "Clientes",
                columns: new[] { "BarberiaId", "Telefono" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_BarberiaId",
                table: "AspNetUsers",
                column: "BarberiaId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Barberias_BarberiaId",
                table: "AspNetUsers",
                column: "BarberiaId",
                principalTable: "Barberias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Clientes_Barberias_BarberiaId",
                table: "Clientes",
                column: "BarberiaId",
                principalTable: "Barberias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Barberias_BarberiaId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Clientes_Barberias_BarberiaId",
                table: "Clientes");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_BarberiaId_Telefono",
                table: "Clientes");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_BarberiaId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "BarberiaId",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "BarberiaId",
                table: "AspNetUsers");
        }
    }
}
