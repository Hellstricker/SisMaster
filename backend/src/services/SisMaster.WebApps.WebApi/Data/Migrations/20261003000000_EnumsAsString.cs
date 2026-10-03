using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisMaster.WebApps.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnumsAsString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Partidas.Status: int → nvarchar(20) ──────────────────────────
            migrationBuilder.AddColumn<string>(
                name: "Status_str",
                table: "Partidas",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NaoIniciada");

            migrationBuilder.Sql(@"
                UPDATE Partidas SET Status_str = CASE Status
                    WHEN 0 THEN 'NaoIniciada'
                    WHEN 1 THEN 'EmAndamento'
                    WHEN 2 THEN 'Intervalo'
                    WHEN 3 THEN 'Encerrada'
                    ELSE 'NaoIniciada'
                END");

            migrationBuilder.DropColumn(name: "Status", table: "Partidas");
            migrationBuilder.RenameColumn(name: "Status_str", table: "Partidas", newName: "Status");

            // ── Partidas.PeriodoAtual: int → nvarchar(20) ────────────────────
            migrationBuilder.AddColumn<string>(
                name: "PeriodoAtual_str",
                table: "Partidas",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Primeiro");

            migrationBuilder.Sql(@"
                UPDATE Partidas SET PeriodoAtual_str = CASE PeriodoAtual
                    WHEN 1 THEN 'Primeiro'
                    WHEN 2 THEN 'Segundo'
                    WHEN 3 THEN 'Terceiro'
                    WHEN 4 THEN 'Quarto'
                    WHEN 5 THEN 'Prorrogacao'
                    ELSE 'Primeiro'
                END");

            migrationBuilder.DropColumn(name: "PeriodoAtual", table: "Partidas");
            migrationBuilder.RenameColumn(name: "PeriodoAtual_str", table: "Partidas", newName: "PeriodoAtual");

            // ── EventosPartida.Tipo: int → nvarchar(30) ──────────────────────
            migrationBuilder.AddColumn<string>(
                name: "Tipo_str",
                table: "EventosPartida",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
                UPDATE EventosPartida SET Tipo_str = CASE Tipo
                    WHEN 1  THEN 'Ponto2'
                    WHEN 2  THEN 'Ponto3'
                    WHEN 3  THEN 'LanceLivre'
                    WHEN 4  THEN 'Erro2'
                    WHEN 5  THEN 'Erro3'
                    WHEN 6  THEN 'ErroLance'
                    WHEN 7  THEN 'ReboteOfensivo'
                    WHEN 8  THEN 'ReboteDefensivo'
                    WHEN 9  THEN 'Assistencia'
                    WHEN 10 THEN 'Roubada'
                    WHEN 11 THEN 'Toco'
                    WHEN 12 THEN 'FaltaPessoal'
                    WHEN 13 THEN 'FaltaTecnica'
                    WHEN 14 THEN 'FaltaAntiDesportiva'
                    WHEN 15 THEN 'Turnover'
                    ELSE ''
                END");

            migrationBuilder.DropColumn(name: "Tipo", table: "EventosPartida");
            migrationBuilder.RenameColumn(name: "Tipo_str", table: "EventosPartida", newName: "Tipo");

            // ── EventosPartida.Periodo: int → nvarchar(20) ───────────────────
            migrationBuilder.AddColumn<string>(
                name: "Periodo_str",
                table: "EventosPartida",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
                UPDATE EventosPartida SET Periodo_str = CASE Periodo
                    WHEN 1 THEN 'Primeiro'
                    WHEN 2 THEN 'Segundo'
                    WHEN 3 THEN 'Terceiro'
                    WHEN 4 THEN 'Quarto'
                    WHEN 5 THEN 'Prorrogacao'
                    ELSE ''
                END");

            migrationBuilder.DropColumn(name: "Periodo", table: "EventosPartida");
            migrationBuilder.RenameColumn(name: "Periodo_str", table: "EventosPartida", newName: "Periodo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── EventosPartida.Periodo: nvarchar(20) → int ───────────────────
            migrationBuilder.AddColumn<int>(name: "Periodo_int", table: "EventosPartida", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.Sql(@"
                UPDATE EventosPartida SET Periodo_int = CASE Periodo
                    WHEN 'Primeiro'    THEN 1
                    WHEN 'Segundo'     THEN 2
                    WHEN 'Terceiro'    THEN 3
                    WHEN 'Quarto'      THEN 4
                    WHEN 'Prorrogacao' THEN 5
                    ELSE 0
                END");
            migrationBuilder.DropColumn(name: "Periodo", table: "EventosPartida");
            migrationBuilder.RenameColumn(name: "Periodo_int", table: "EventosPartida", newName: "Periodo");

            // ── EventosPartida.Tipo: nvarchar(30) → int ──────────────────────
            migrationBuilder.AddColumn<int>(name: "Tipo_int", table: "EventosPartida", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.Sql(@"
                UPDATE EventosPartida SET Tipo_int = CASE Tipo
                    WHEN 'Ponto2'             THEN 1
                    WHEN 'Ponto3'             THEN 2
                    WHEN 'LanceLivre'         THEN 3
                    WHEN 'Erro2'              THEN 4
                    WHEN 'Erro3'              THEN 5
                    WHEN 'ErroLance'          THEN 6
                    WHEN 'ReboteOfensivo'     THEN 7
                    WHEN 'ReboteDefensivo'    THEN 8
                    WHEN 'Assistencia'        THEN 9
                    WHEN 'Roubada'            THEN 10
                    WHEN 'Toco'               THEN 11
                    WHEN 'FaltaPessoal'       THEN 12
                    WHEN 'FaltaTecnica'       THEN 13
                    WHEN 'FaltaAntiDesportiva' THEN 14
                    WHEN 'Turnover'           THEN 15
                    ELSE 0
                END");
            migrationBuilder.DropColumn(name: "Tipo", table: "EventosPartida");
            migrationBuilder.RenameColumn(name: "Tipo_int", table: "EventosPartida", newName: "Tipo");

            // ── Partidas.PeriodoAtual: nvarchar(20) → int ────────────────────
            migrationBuilder.AddColumn<int>(name: "PeriodoAtual_int", table: "Partidas", type: "int", nullable: false, defaultValue: 1);
            migrationBuilder.Sql(@"
                UPDATE Partidas SET PeriodoAtual_int = CASE PeriodoAtual
                    WHEN 'Primeiro'    THEN 1
                    WHEN 'Segundo'     THEN 2
                    WHEN 'Terceiro'    THEN 3
                    WHEN 'Quarto'      THEN 4
                    WHEN 'Prorrogacao' THEN 5
                    ELSE 1
                END");
            migrationBuilder.DropColumn(name: "PeriodoAtual", table: "Partidas");
            migrationBuilder.RenameColumn(name: "PeriodoAtual_int", table: "Partidas", newName: "PeriodoAtual");

            // ── Partidas.Status: nvarchar(20) → int ──────────────────────────
            migrationBuilder.AddColumn<int>(name: "Status_int", table: "Partidas", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.Sql(@"
                UPDATE Partidas SET Status_int = CASE Status
                    WHEN 'NaoIniciada'  THEN 0
                    WHEN 'EmAndamento'  THEN 1
                    WHEN 'Intervalo'    THEN 2
                    WHEN 'Encerrada'    THEN 3
                    ELSE 0
                END");
            migrationBuilder.DropColumn(name: "Status", table: "Partidas");
            migrationBuilder.RenameColumn(name: "Status_int", table: "Partidas", newName: "Status");
        }
    }
}
