using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisMaster.WebApps.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AtletasTardiosEEstadoEmPreparacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O estado Preparada passou a se chamar EmPreparacao (o status é gravado como texto).
            migrationBuilder.Sql("UPDATE Sumulas SET Status = N'EmPreparacao' WHERE Status = N'Preparada'");

            migrationBuilder.AddColumn<bool>(
                name: "ChegouNoInicio",
                table: "SumulaJogadores",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "ChegouNoPeriodo",
                table: "SumulaJogadores",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "JogadorSaiId",
                table: "SubstituicoesSumula",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Sumulas SET Status = N'Preparada' WHERE Status = N'EmPreparacao'");

            migrationBuilder.DropColumn(
                name: "ChegouNoInicio",
                table: "SumulaJogadores");

            migrationBuilder.DropColumn(
                name: "ChegouNoPeriodo",
                table: "SumulaJogadores");

            migrationBuilder.AlterColumn<Guid>(
                name: "JogadorSaiId",
                table: "SubstituicoesSumula",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
