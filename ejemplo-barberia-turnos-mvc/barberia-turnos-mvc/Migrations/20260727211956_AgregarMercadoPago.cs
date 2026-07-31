using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace barberia_turnos_mvc.Migrations
{
    /// <inheritdoc />
    public partial class AgregarMercadoPago : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MercadoPagoPaymentId",
                table: "Turnos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MercadoPagoPreferenceId",
                table: "Turnos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MontoSeña",
                table: "Turnos",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SeñaPagada",
                table: "Turnos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PorcentajeSeña",
                table: "Barberias",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MercadoPagoPaymentId",
                table: "Turnos");

            migrationBuilder.DropColumn(
                name: "MercadoPagoPreferenceId",
                table: "Turnos");

            migrationBuilder.DropColumn(
                name: "MontoSeña",
                table: "Turnos");

            migrationBuilder.DropColumn(
                name: "SeñaPagada",
                table: "Turnos");

            migrationBuilder.DropColumn(
                name: "PorcentajeSeña",
                table: "Barberias");
        }
    }
}
