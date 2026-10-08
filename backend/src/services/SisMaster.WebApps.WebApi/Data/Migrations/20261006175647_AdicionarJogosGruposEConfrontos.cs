using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisMaster.WebApps.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarJogosGruposEConfrontos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Confrontos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    OrigemA_Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OrigemA_EquipeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrigemA_FaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrigemA_GrupoOrdem = table.Column<int>(type: "int", nullable: true),
                    OrigemA_Posicao = table.Column<int>(type: "int", nullable: true),
                    OrigemA_ConfrontoOrigemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrigemB_Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OrigemB_EquipeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrigemB_FaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrigemB_GrupoOrdem = table.Column<int>(type: "int", nullable: true),
                    OrigemB_Posicao = table.Column<int>(type: "int", nullable: true),
                    OrigemB_ConfrontoOrigemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Confrontos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Confrontos_Fases_FaseId",
                        column: x => x.FaseId,
                        principalTable: "Fases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Grupos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grupos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Grupos_Fases_FaseId",
                        column: x => x.FaseId,
                        principalTable: "Fases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Locais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssociacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Cidade = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Locais_Associacoes_AssociacaoId",
                        column: x => x.AssociacaoId,
                        principalTable: "Associacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FaseEquipes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GrupoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Posicao = table.Column<int>(type: "int", nullable: false),
                    Origem_Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Origem_EquipeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Origem_FaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Origem_GrupoOrdem = table.Column<int>(type: "int", nullable: true),
                    Origem_Posicao = table.Column<int>(type: "int", nullable: true),
                    Origem_ConfrontoOrigemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EquipeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaseEquipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaseEquipes_Equipes_EquipeId",
                        column: x => x.EquipeId,
                        principalTable: "Equipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FaseEquipes_Fases_FaseId",
                        column: x => x.FaseId,
                        principalTable: "Fases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FaseEquipes_Grupos_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "Grupos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Jogos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemporadaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GrupoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfrontoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Rodada = table.Column<int>(type: "int", nullable: false),
                    JogoDaSerie = table.Column<int>(type: "int", nullable: true),
                    Opcional = table.Column<bool>(type: "bit", nullable: false),
                    CasaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VisitanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: true),
                    Hora = table.Column<TimeOnly>(type: "time", nullable: true),
                    LocalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PlacarCasa = table.Column<int>(type: "int", nullable: true),
                    PlacarVisitante = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jogos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Jogos_Confrontos_ConfrontoId",
                        column: x => x.ConfrontoId,
                        principalTable: "Confrontos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Jogos_FaseEquipes_CasaId",
                        column: x => x.CasaId,
                        principalTable: "FaseEquipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Jogos_FaseEquipes_VisitanteId",
                        column: x => x.VisitanteId,
                        principalTable: "FaseEquipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Jogos_Fases_FaseId",
                        column: x => x.FaseId,
                        principalTable: "Fases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Jogos_Grupos_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "Grupos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Jogos_Locais_LocalId",
                        column: x => x.LocalId,
                        principalTable: "Locais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Jogos_Temporadas_TemporadaId",
                        column: x => x.TemporadaId,
                        principalTable: "Temporadas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Confrontos_FaseId_Numero",
                table: "Confrontos",
                columns: new[] { "FaseId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FaseEquipes_EquipeId",
                table: "FaseEquipes",
                column: "EquipeId");

            migrationBuilder.CreateIndex(
                name: "IX_FaseEquipes_FaseId_Posicao",
                table: "FaseEquipes",
                columns: new[] { "FaseId", "Posicao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FaseEquipes_GrupoId",
                table: "FaseEquipes",
                column: "GrupoId");

            migrationBuilder.CreateIndex(
                name: "IX_Grupos_FaseId_Ordem",
                table: "Grupos",
                columns: new[] { "FaseId", "Ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jogos_CasaId",
                table: "Jogos",
                column: "CasaId");

            migrationBuilder.CreateIndex(
                name: "IX_Jogos_ConfrontoId",
                table: "Jogos",
                column: "ConfrontoId");

            migrationBuilder.CreateIndex(
                name: "IX_Jogos_FaseId",
                table: "Jogos",
                column: "FaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Jogos_GrupoId",
                table: "Jogos",
                column: "GrupoId");

            migrationBuilder.CreateIndex(
                name: "IX_Jogos_LocalId",
                table: "Jogos",
                column: "LocalId");

            migrationBuilder.CreateIndex(
                name: "IX_Jogos_TemporadaId_Numero",
                table: "Jogos",
                columns: new[] { "TemporadaId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jogos_VisitanteId",
                table: "Jogos",
                column: "VisitanteId");

            migrationBuilder.CreateIndex(
                name: "IX_Locais_AssociacaoId_Nome_Cidade",
                table: "Locais",
                columns: new[] { "AssociacaoId", "Nome", "Cidade" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Jogos");

            migrationBuilder.DropTable(
                name: "Confrontos");

            migrationBuilder.DropTable(
                name: "FaseEquipes");

            migrationBuilder.DropTable(
                name: "Locais");

            migrationBuilder.DropTable(
                name: "Grupos");
        }
    }
}
