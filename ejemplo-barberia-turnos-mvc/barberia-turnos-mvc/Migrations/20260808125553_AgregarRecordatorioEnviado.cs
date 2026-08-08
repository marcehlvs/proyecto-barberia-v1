using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace barberia_turnos_mvc.Migrations
{
    /// <inheritdoc />
    public partial class AgregarRecordatorioEnviado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RecordatorioEnviado",
                table: "Turnos",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecordatorioEnviado",
                table: "Turnos");
        }
    }
}
