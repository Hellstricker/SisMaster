using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisMaster.WebApps.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Associacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Uf = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Ativa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Associacoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Partidas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampeonatoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimeCasaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimeVisitanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PeriodoAtual = table.Column<int>(type: "int", nullable: false),
                    PlacarCasa = table.Column<int>(type: "int", nullable: false),
                    PlacarVisitante = table.Column<int>(type: "int", nullable: false),
                    FaltasCasa = table.Column<int>(type: "int", nullable: false),
                    FaltasVisitante = table.Column<int>(type: "int", nullable: false),
                    CronometroAtivo = table.Column<bool>(type: "bit", nullable: false),
                    CronometroSegundosRestantes = table.Column<int>(type: "int", nullable: false),
                    CronometroIniciadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CronometroSegundosAoIniciar = table.Column<int>(type: "int", nullable: false),
                    ShotClockAtivo = table.Column<bool>(type: "bit", nullable: false),
                    ShotClockSegundosRestantes = table.Column<int>(type: "int", nullable: false),
                    ShotClockIniciadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ShotClockSegundosAoIniciar = table.Column<int>(type: "int", nullable: false),
                    PosseCasa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Partidas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Times",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssociacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Sigla = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Times", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Campeonatos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssociacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Ano = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campeonatos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Campeonatos_Associacoes_AssociacaoId",
                        column: x => x.AssociacaoId,
                        principalTable: "Associacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventosPartida",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartidaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JogadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Periodo = table.Column<int>(type: "int", nullable: false),
                    TempoJogoSegundos = table.Column<int>(type: "int", nullable: false),
                    RegistradoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosPartida", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventosPartida_Partidas_PartidaId",
                        column: x => x.PartidaId,
                        principalTable: "Partidas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Jogadores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Cpf = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jogadores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Jogadores_Times_TimeId",
                        column: x => x.TimeId,
                        principalTable: "Times",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Campeonatos_AssociacaoId",
                table: "Campeonatos",
                column: "AssociacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosPartida_PartidaId",
                table: "EventosPartida",
                column: "PartidaId");

            migrationBuilder.CreateIndex(
                name: "IX_Jogadores_Cpf",
                table: "Jogadores",
                column: "Cpf",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jogadores_TimeId",
                table: "Jogadores",
                column: "TimeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Campeonatos");

            migrationBuilder.DropTable(
                name: "EventosPartida");

            migrationBuilder.DropTable(
                name: "Jogadores");

            migrationBuilder.DropTable(
                name: "Associacoes");

            migrationBuilder.DropTable(
                name: "Partidas");

            migrationBuilder.DropTable(
                name: "Times");
        }
    }
}
