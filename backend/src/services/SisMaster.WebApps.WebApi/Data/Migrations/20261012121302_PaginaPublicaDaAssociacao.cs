using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisMaster.WebApps.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class PaginaPublicaDaAssociacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaginasPublicas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssociacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Missao = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    QuemSomos = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    GestaoDaDiretoria = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Whatsapp = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Instagram = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    OndeJogamos = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaginasPublicas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaginasPublicas_Associacoes_AssociacaoId",
                        column: x => x.AssociacaoId,
                        principalTable: "Associacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MembrosDaDiretoria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaginaPublicaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cargo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Ordem = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembrosDaDiretoria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MembrosDaDiretoria_PaginasPublicas_PaginaPublicaId",
                        column: x => x.PaginaPublicaId,
                        principalTable: "PaginasPublicas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MembrosDaDiretoria_PaginaPublicaId",
                table: "MembrosDaDiretoria",
                column: "PaginaPublicaId");

            migrationBuilder.CreateIndex(
                name: "IX_PaginasPublicas_AssociacaoId",
                table: "PaginasPublicas",
                column: "AssociacaoId",
                unique: true);

            // Conteúdo inicial da ABABAS (rascunho aprovado a partir do PLANO.md do SiteAbabas; a diretoria revisa na gestão).
            // Só se a associação existir e ainda não tiver página. Contatos e demais cargos ficam em branco: não constavam na fonte.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM Associacoes WHERE Sigla = N'ABABAS') AND NOT EXISTS (SELECT 1 FROM PaginasPublicas WHERE AssociacaoId = (SELECT TOP 1 Id FROM Associacoes WHERE Sigla = N'ABABAS'))
BEGIN
    DECLARE @assoc uniqueidentifier = (SELECT TOP 1 Id FROM Associacoes WHERE Sigla = N'ABABAS');
    DECLARE @pagina uniqueidentifier = NEWID();
    INSERT INTO PaginasPublicas (Id, AssociacaoId, Missao, QuemSomos, GestaoDaDiretoria)
    VALUES (@pagina, @assoc, N'Aqui, o basquete da Bahia vive.',
        N'A ABABAS reúne atletas veteranos do basquete master na Bahia e organiza campeonatos desde meados dos anos 1980. Competimos nas categorias Master, Supermaster e Hipermaster, em faixas de idade a partir dos 35 anos, e representamos o estado em campeonatos regionais, brasileiros e mundiais.'
        + NCHAR(10) + NCHAR(10) +
        N'Em 2024, como anfitriã em Salvador, a ABABAS foi a maior campeã do Campeonato Norte-Nordeste, com 10 medalhas (4 de ouro, 1 de prata e 5 de bronze). Em 2025, nove atletas baianos representaram o Brasil no Mundial da FIMBA, na Suíça.',
        N'2025/2026');
    INSERT INTO MembrosDaDiretoria (Id, PaginaPublicaId, Cargo, Nome, Ordem) VALUES
        (NEWID(), @pagina, N'Presidente', N'Ivanilton de Jesus Moreira', 1),
        (NEWID(), @pagina, N'Vice-presidente', N'Ricardo Henrique F. Duarte', 2),
        (NEWID(), @pagina, N'Secretário', NULL, 3),
        (NEWID(), @pagina, N'Tesoureiro', NULL, 4);
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MembrosDaDiretoria");

            migrationBuilder.DropTable(
                name: "PaginasPublicas");
        }
    }
}
