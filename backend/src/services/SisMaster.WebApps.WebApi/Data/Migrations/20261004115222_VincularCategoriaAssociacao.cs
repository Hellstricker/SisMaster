using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisMaster.WebApps.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class VincularCategoriaAssociacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TemporadaCategorias_TemporadaId",
                table: "TemporadaCategorias");

            migrationBuilder.AddColumn<bool>(
                name: "InscricoesHabilitadas",
                table: "Temporadas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorBonificacaoPorAtleta",
                table: "Temporadas",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "IdadeMinima",
                table: "TemporadaCategorias",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinimoPeriodosEmQuadra",
                table: "TemporadaCategorias",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinimoPeriodosForaQuadra",
                table: "TemporadaCategorias",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Nome",
                table: "TemporadaCategorias",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Sexo",
                table: "TemporadaCategorias",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Valor",
                table: "TemporadaCategorias",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssociacaoId",
                table: "Categorias",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "IdadeMinima",
                table: "Categorias",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinimoPeriodosEmQuadra",
                table: "Categorias",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinimoPeriodosForaQuadra",
                table: "Categorias",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Sexo",
                table: "Categorias",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            // Backfill dos dados existentes antes dos índices/FK: rodízio padrão 1/1, snapshot
            // copiado da Categoria, associação inferida pela temporada em que a categoria é usada
            // e inscrições habilitadas apenas nas temporadas ainda com inscrições abertas (Status 0).
            migrationBuilder.Sql("UPDATE Categorias SET MinimoPeriodosEmQuadra = 1, MinimoPeriodosForaQuadra = 1");
            migrationBuilder.Sql(@"UPDATE tc SET tc.Nome = c.Nome, tc.MinimoPeriodosEmQuadra = 1, tc.MinimoPeriodosForaQuadra = 1
                                   FROM TemporadaCategorias tc JOIN Categorias c ON c.Id = tc.CategoriaId");
            migrationBuilder.Sql(@"UPDATE c SET c.AssociacaoId = x.AssociacaoId
                                   FROM Categorias c
                                   CROSS APPLY (SELECT TOP 1 camp.AssociacaoId
                                                FROM TemporadaCategorias tc
                                                JOIN Temporadas t ON t.Id = tc.TemporadaId
                                                JOIN Campeonatos camp ON camp.Id = t.CampeonatoId
                                                WHERE tc.CategoriaId = c.Id) x");
            migrationBuilder.Sql("UPDATE Temporadas SET InscricoesHabilitadas = CASE WHEN Status = 0 THEN 1 ELSE 0 END");
            // Categorias que nenhuma temporada usa não permitem inferir a associação: ficam com
            // a mesma associação das categorias já inferidas (ou a primeira associação, se nenhuma).
            migrationBuilder.Sql(@"UPDATE Categorias SET AssociacaoId = COALESCE(
                                       (SELECT TOP 1 AssociacaoId FROM Categorias
                                        WHERE AssociacaoId <> '00000000-0000-0000-0000-000000000000'),
                                       (SELECT TOP 1 Id FROM Associacoes ORDER BY Nome))
                                   WHERE AssociacaoId = '00000000-0000-0000-0000-000000000000'");

            migrationBuilder.CreateIndex(
                name: "IX_TemporadaCategorias_TemporadaId_CategoriaId",
                table: "TemporadaCategorias",
                columns: new[] { "TemporadaId", "CategoriaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categorias_AssociacaoId_Nome",
                table: "Categorias",
                columns: new[] { "AssociacaoId", "Nome" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Categorias_Associacoes_AssociacaoId",
                table: "Categorias",
                column: "AssociacaoId",
                principalTable: "Associacoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categorias_Associacoes_AssociacaoId",
                table: "Categorias");

            migrationBuilder.DropIndex(
                name: "IX_TemporadaCategorias_TemporadaId_CategoriaId",
                table: "TemporadaCategorias");

            migrationBuilder.DropIndex(
                name: "IX_Categorias_AssociacaoId_Nome",
                table: "Categorias");

            migrationBuilder.DropColumn(
                name: "InscricoesHabilitadas",
                table: "Temporadas");

            migrationBuilder.DropColumn(
                name: "ValorBonificacaoPorAtleta",
                table: "Temporadas");

            migrationBuilder.DropColumn(
                name: "IdadeMinima",
                table: "TemporadaCategorias");

            migrationBuilder.DropColumn(
                name: "MinimoPeriodosEmQuadra",
                table: "TemporadaCategorias");

            migrationBuilder.DropColumn(
                name: "MinimoPeriodosForaQuadra",
                table: "TemporadaCategorias");

            migrationBuilder.DropColumn(
                name: "Nome",
                table: "TemporadaCategorias");

            migrationBuilder.DropColumn(
                name: "Sexo",
                table: "TemporadaCategorias");

            migrationBuilder.DropColumn(
                name: "Valor",
                table: "TemporadaCategorias");

            migrationBuilder.DropColumn(
                name: "AssociacaoId",
                table: "Categorias");

            migrationBuilder.DropColumn(
                name: "IdadeMinima",
                table: "Categorias");

            migrationBuilder.DropColumn(
                name: "MinimoPeriodosEmQuadra",
                table: "Categorias");

            migrationBuilder.DropColumn(
                name: "MinimoPeriodosForaQuadra",
                table: "Categorias");

            migrationBuilder.DropColumn(
                name: "Sexo",
                table: "Categorias");

            migrationBuilder.CreateIndex(
                name: "IX_TemporadaCategorias_TemporadaId",
                table: "TemporadaCategorias",
                column: "TemporadaId");
        }
    }
}
