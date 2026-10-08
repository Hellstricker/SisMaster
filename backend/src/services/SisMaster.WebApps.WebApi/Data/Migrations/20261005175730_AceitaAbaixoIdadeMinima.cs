using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisMaster.WebApps.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AceitaAbaixoIdadeMinima : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AceitaAbaixoIdadeMinima",
                table: "TemporadaCategorias",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AceitaAbaixoIdadeMinima",
                table: "Categorias",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AceitaAbaixoIdadeMinima",
                table: "TemporadaCategorias");

            migrationBuilder.DropColumn(
                name: "AceitaAbaixoIdadeMinima",
                table: "Categorias");
        }
    }
}
