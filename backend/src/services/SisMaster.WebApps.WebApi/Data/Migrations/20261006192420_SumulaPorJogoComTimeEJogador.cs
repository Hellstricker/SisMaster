using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisMaster.WebApps.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class SumulaPorJogoComTimeEJogador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventosPartida");

            migrationBuilder.DropTable(
                name: "Jogadores");

            migrationBuilder.DropTable(
                name: "Partidas");

            migrationBuilder.DropTable(
                name: "Times");

            migrationBuilder.AddColumn<Guid>(
                name: "SumulaId",
                table: "Jogos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Sumulas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JogoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimeCasaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimeVisitanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PeriodoAtual = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_Sumulas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventosSumula",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SumulaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JogadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Periodo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TempoJogoSegundos = table.Column<int>(type: "int", nullable: false),
                    RegistradoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosSumula", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventosSumula_Sumulas_SumulaId",
                        column: x => x.SumulaId,
                        principalTable: "Sumulas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SumulaTimes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SumulaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Lado = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EquipeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Cor = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    Tecnico = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AuxiliarTecnico = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CapitaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SumulaTimes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SumulaTimes_Sumulas_SumulaId",
                        column: x => x.SumulaId,
                        principalTable: "Sumulas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SumulaJogadores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AtletaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Titular = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SumulaJogadores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SumulaJogadores_SumulaTimes_TimeId",
                        column: x => x.TimeId,
                        principalTable: "SumulaTimes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventosSumula_SumulaId",
                table: "EventosSumula",
                column: "SumulaId");

            migrationBuilder.CreateIndex(
                name: "IX_SumulaJogadores_TimeId_AtletaId",
                table: "SumulaJogadores",
                columns: new[] { "TimeId", "AtletaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SumulaJogadores_TimeId_Numero",
                table: "SumulaJogadores",
                columns: new[] { "TimeId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sumulas_JogoId",
                table: "Sumulas",
                column: "JogoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SumulaTimes_SumulaId_Lado",
                table: "SumulaTimes",
                columns: new[] { "SumulaId", "Lado" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventosSumula");

            migrationBuilder.DropTable(
                name: "SumulaJogadores");

            migrationBuilder.DropTable(
                name: "SumulaTimes");

            migrationBuilder.DropTable(
                name: "Sumulas");

            migrationBuilder.DropColumn(
                name: "SumulaId",
                table: "Jogos");

            migrationBuilder.CreateTable(
                name: "Partidas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampeonatoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CronometroAtivo = table.Column<bool>(type: "bit", nullable: false),
                    CronometroIniciadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CronometroSegundosAoIniciar = table.Column<int>(type: "int", nullable: false),
                    CronometroSegundosRestantes = table.Column<int>(type: "int", nullable: false),
                    FaltasCasa = table.Column<int>(type: "int", nullable: false),
                    FaltasVisitante = table.Column<int>(type: "int", nullable: false),
                    PeriodoAtual = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PlacarCasa = table.Column<int>(type: "int", nullable: false),
                    PlacarVisitante = table.Column<int>(type: "int", nullable: false),
                    PosseCasa = table.Column<bool>(type: "bit", nullable: false),
                    ShotClockAtivo = table.Column<bool>(type: "bit", nullable: false),
                    ShotClockIniciadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ShotClockSegundosAoIniciar = table.Column<int>(type: "int", nullable: false),
                    ShotClockSegundosRestantes = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TimeCasaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimeVisitanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
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
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Sigla = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Times", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventosPartida",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartidaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JogadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Periodo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RegistradoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TempoJogoSegundos = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
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
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
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
    }
}
