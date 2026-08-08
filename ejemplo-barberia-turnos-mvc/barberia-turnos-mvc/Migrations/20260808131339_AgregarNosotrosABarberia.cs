using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace barberia_turnos_mvc.Migrations
{
    /// <inheritdoc />
    public partial class AgregarNosotrosABarberia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Descripcion",
                table: "Barberias",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FotosUrls",
                table: "Barberias",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Descripcion",
                table: "Barberias");

            migrationBuilder.DropColumn(
                name: "FotosUrls",
                table: "Barberias");
        }
    }
}
