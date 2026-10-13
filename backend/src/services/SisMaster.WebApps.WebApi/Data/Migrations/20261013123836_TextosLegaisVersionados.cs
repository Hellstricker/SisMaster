using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisMaster.WebApps.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class TextosLegaisVersionados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VersoesLegais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssociacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Versao = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Relevante = table.Column<bool>(type: "bit", nullable: false),
                    Resumo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    TextoPrivacidade = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TextoTermos = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VigenteDesde = table.Column<DateOnly>(type: "date", nullable: false),
                    PublicadaPor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PublicadaEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VersoesLegais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VersoesLegais_Associacoes_AssociacaoId",
                        column: x => x.AssociacaoId,
                        principalTable: "Associacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VersoesLegais_AssociacaoId_Numero",
                table: "VersoesLegais",
                columns: new[] { "AssociacaoId", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VersoesLegais");
        }
    }
}
