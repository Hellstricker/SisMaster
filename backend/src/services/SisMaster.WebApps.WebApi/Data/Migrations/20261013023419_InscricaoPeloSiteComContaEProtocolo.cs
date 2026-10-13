using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisMaster.WebApps.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class InscricaoPeloSiteComContaEProtocolo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContaId",
                table: "Pessoas",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CodigoDeEnvio",
                table: "Inscricoes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsentimentoImagemEm",
                table: "Inscricoes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Origem",
                table: "Inscricoes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Diretoria");

            migrationBuilder.AddColumn<string>(
                name: "Protocolo",
                table: "Inscricoes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VersaoTermos",
                table: "Inscricoes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pessoas_ContaId",
                table: "Pessoas",
                column: "ContaId",
                unique: true,
                filter: "[ContaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Inscricoes_CodigoDeEnvio",
                table: "Inscricoes",
                column: "CodigoDeEnvio",
                unique: true,
                filter: "[CodigoDeEnvio] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Inscricoes_TemporadaId_Protocolo",
                table: "Inscricoes",
                columns: new[] { "TemporadaId", "Protocolo" },
                unique: true,
                filter: "[Protocolo] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pessoas_ContaId",
                table: "Pessoas");

            migrationBuilder.DropIndex(
                name: "IX_Inscricoes_CodigoDeEnvio",
                table: "Inscricoes");

            migrationBuilder.DropIndex(
                name: "IX_Inscricoes_TemporadaId_Protocolo",
                table: "Inscricoes");

            migrationBuilder.DropColumn(
                name: "ContaId",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "CodigoDeEnvio",
                table: "Inscricoes");

            migrationBuilder.DropColumn(
                name: "ConsentimentoImagemEm",
                table: "Inscricoes");

            migrationBuilder.DropColumn(
                name: "Origem",
                table: "Inscricoes");

            migrationBuilder.DropColumn(
                name: "Protocolo",
                table: "Inscricoes");

            migrationBuilder.DropColumn(
                name: "VersaoTermos",
                table: "Inscricoes");
        }
    }
}
