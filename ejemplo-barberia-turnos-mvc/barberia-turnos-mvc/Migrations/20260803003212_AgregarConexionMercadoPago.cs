using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace barberia_turnos_mvc.Migrations
{
    /// <inheritdoc />
    public partial class AgregarConexionMercadoPago : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MercadoPagoAccessToken",
                table: "Barberias",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MercadoPagoPublicKey",
                table: "Barberias",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MercadoPagoRefreshToken",
                table: "Barberias",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MercadoPagoTokenExpira",
                table: "Barberias",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MercadoPagoUserId",
                table: "Barberias",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MercadoPagoAccessToken",
                table: "Barberias");

            migrationBuilder.DropColumn(
                name: "MercadoPagoPublicKey",
                table: "Barberias");

            migrationBuilder.DropColumn(
                name: "MercadoPagoRefreshToken",
                table: "Barberias");

            migrationBuilder.DropColumn(
                name: "MercadoPagoTokenExpira",
                table: "Barberias");

            migrationBuilder.DropColumn(
                name: "MercadoPagoUserId",
                table: "Barberias");
        }
    }
}
