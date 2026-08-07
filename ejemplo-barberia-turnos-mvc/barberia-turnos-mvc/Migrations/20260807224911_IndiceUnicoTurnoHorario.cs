using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace barberia_turnos_mvc.Migrations
{
    /// <inheritdoc />
    public partial class IndiceUnicoTurnoHorario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Turnos_BarberiaId",
                table: "Turnos");

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_BarberiaId_FechaHora",
                table: "Turnos",
                columns: new[] { "BarberiaId", "FechaHora" },
                unique: true,
                filter: "[Estado] <> 2 AND [Estado] <> 4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Turnos_BarberiaId_FechaHora",
                table: "Turnos");

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_BarberiaId",
                table: "Turnos",
                column: "BarberiaId");
        }
    }
}
