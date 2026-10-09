using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisMaster.WebApps.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class InscricoesG7AuditoriaEEstornos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EstornadoEm",
                table: "PagamentosInscricao",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstornadoPor",
                table: "PagamentosInscricao",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoEstorno",
                table: "PagamentosInscricao",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RegistrosDeAuditoria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Em = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Acao = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Entidade = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    EntidadeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Detalhe = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosDeAuditoria", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosDeAuditoria_Em",
                table: "RegistrosDeAuditoria",
                column: "Em");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosDeAuditoria_Entidade_EntidadeId",
                table: "RegistrosDeAuditoria",
                columns: new[] { "Entidade", "EntidadeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegistrosDeAuditoria");

            migrationBuilder.DropColumn(
                name: "EstornadoEm",
                table: "PagamentosInscricao");

            migrationBuilder.DropColumn(
                name: "EstornadoPor",
                table: "PagamentosInscricao");

            migrationBuilder.DropColumn(
                name: "MotivoEstorno",
                table: "PagamentosInscricao");
        }
    }
}
