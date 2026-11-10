using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MarcasOperacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "marcas_operacao",
                columns: table => new
                {
                    chave = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_marcas_operacao", x => x.chave);
                });

            migrationBuilder.CreateIndex(
                name: "ix_marcas_operacao_criada_em",
                table: "marcas_operacao",
                column: "criada_em");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "marcas_operacao");
        }
    }
}
