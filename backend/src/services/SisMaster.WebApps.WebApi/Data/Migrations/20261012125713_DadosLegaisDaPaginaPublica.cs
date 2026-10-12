using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisMaster.WebApps.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class DadosLegaisDaPaginaPublica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cnpj",
                table: "PaginasPublicas",
                type: "nvarchar(18)",
                maxLength: 18,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailPrivacidade",
                table: "PaginasPublicas",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncarregadoDeDados",
                table: "PaginasPublicas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Endereco",
                table: "PaginasPublicas",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ForoCidade",
                table: "PaginasPublicas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cnpj",
                table: "PaginasPublicas");

            migrationBuilder.DropColumn(
                name: "EmailPrivacidade",
                table: "PaginasPublicas");

            migrationBuilder.DropColumn(
                name: "EncarregadoDeDados",
                table: "PaginasPublicas");

            migrationBuilder.DropColumn(
                name: "Endereco",
                table: "PaginasPublicas");

            migrationBuilder.DropColumn(
                name: "ForoCidade",
                table: "PaginasPublicas");
        }
    }
}
