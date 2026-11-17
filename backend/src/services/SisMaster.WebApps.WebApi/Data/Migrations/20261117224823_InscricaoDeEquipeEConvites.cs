using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisMaster.WebApps.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class InscricaoDeEquipeEConvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InscricoesDeEquipe",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemporadaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemporadaCategoriaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContaId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ResponsavelNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ResponsavelEmail = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ResponsavelTelefone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Protocolo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CodigoDeEnvio = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataEnvio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MotivoRecusa = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DecididaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    JustificativaExcecao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EquipeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InscricoesDeEquipe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InscricoesDeEquipe_Equipes_EquipeId",
                        column: x => x.EquipeId,
                        principalTable: "Equipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InscricoesDeEquipe_TemporadaCategorias_TemporadaCategoriaId",
                        column: x => x.TemporadaCategoriaId,
                        principalTable: "TemporadaCategorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InscricoesDeEquipe_Temporadas_TemporadaId",
                        column: x => x.TemporadaId,
                        principalTable: "Temporadas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConvitesDeAtleta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InscricaoDeEquipeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Cpf = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: true),
                    Nascimento = table.Column<DateOnly>(type: "date", nullable: true),
                    Sexo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    EhResponsavel = table.Column<bool>(type: "bit", nullable: false),
                    CodigoHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ValidoAte = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RespondidoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InscricaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PessoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConvitesDeAtleta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConvitesDeAtleta_InscricoesDeEquipe_InscricaoDeEquipeId",
                        column: x => x.InscricaoDeEquipeId,
                        principalTable: "InscricoesDeEquipe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConvitesDeAtleta_CodigoHash",
                table: "ConvitesDeAtleta",
                column: "CodigoHash");

            migrationBuilder.CreateIndex(
                name: "IX_ConvitesDeAtleta_InscricaoDeEquipeId",
                table: "ConvitesDeAtleta",
                column: "InscricaoDeEquipeId");

            migrationBuilder.CreateIndex(
                name: "IX_ConvitesDeAtleta_InscricaoId",
                table: "ConvitesDeAtleta",
                column: "InscricaoId");

            migrationBuilder.CreateIndex(
                name: "IX_InscricoesDeEquipe_CodigoDeEnvio",
                table: "InscricoesDeEquipe",
                column: "CodigoDeEnvio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InscricoesDeEquipe_ContaId",
                table: "InscricoesDeEquipe",
                column: "ContaId");

            migrationBuilder.CreateIndex(
                name: "IX_InscricoesDeEquipe_EquipeId",
                table: "InscricoesDeEquipe",
                column: "EquipeId");

            migrationBuilder.CreateIndex(
                name: "IX_InscricoesDeEquipe_TemporadaCategoriaId",
                table: "InscricoesDeEquipe",
                column: "TemporadaCategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_InscricoesDeEquipe_TemporadaId_Protocolo",
                table: "InscricoesDeEquipe",
                columns: new[] { "TemporadaId", "Protocolo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConvitesDeAtleta");

            migrationBuilder.DropTable(
                name: "InscricoesDeEquipe");
        }
    }
}
