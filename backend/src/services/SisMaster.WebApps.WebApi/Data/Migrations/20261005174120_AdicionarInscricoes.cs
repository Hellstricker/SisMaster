using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisMaster.WebApps.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarInscricoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pessoas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Cpf = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    Nascimento = table.Column<DateOnly>(type: "date", nullable: false),
                    Sexo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Telefone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Perfil = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pessoas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inscricoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemporadaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlturaCm = table.Column<int>(type: "int", nullable: true),
                    PesoKg = table.Column<int>(type: "int", nullable: true),
                    Posicao = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PossuiPlanoSaude = table.Column<bool>(type: "bit", nullable: false),
                    NomePlanoSaude = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DataEnvio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsentimentoLgpdEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inscricoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inscricoes_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inscricoes_Temporadas_TemporadaId",
                        column: x => x.TemporadaId,
                        principalTable: "Temporadas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InscricoesCategorias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InscricaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemporadaCategoriaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    DataPagamento = table.Column<DateOnly>(type: "date", nullable: true),
                    JustificativaExcecao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RecusadaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoRecusa = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InscricoesCategorias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InscricoesCategorias_Inscricoes_InscricaoId",
                        column: x => x.InscricaoId,
                        principalTable: "Inscricoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InscricoesCategorias_TemporadaCategorias_TemporadaCategoriaId",
                        column: x => x.TemporadaCategoriaId,
                        principalTable: "TemporadaCategorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Inscricoes_PessoaId",
                table: "Inscricoes",
                column: "PessoaId");

            migrationBuilder.CreateIndex(
                name: "IX_Inscricoes_TemporadaId_PessoaId",
                table: "Inscricoes",
                columns: new[] { "TemporadaId", "PessoaId" });

            migrationBuilder.CreateIndex(
                name: "IX_InscricoesCategorias_InscricaoId",
                table: "InscricoesCategorias",
                column: "InscricaoId");

            migrationBuilder.CreateIndex(
                name: "IX_InscricoesCategorias_TemporadaCategoriaId_Status",
                table: "InscricoesCategorias",
                columns: new[] { "TemporadaCategoriaId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Pessoas_Cpf",
                table: "Pessoas",
                column: "Cpf",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InscricoesCategorias");

            migrationBuilder.DropTable(
                name: "Inscricoes");

            migrationBuilder.DropTable(
                name: "Pessoas");
        }
    }
}
