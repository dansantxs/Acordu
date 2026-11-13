using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SituacaoDaEmpresa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cancelada_em",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fim_do_teste",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "situacao",
                table: "tenants",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "somente_leitura_desde",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            // Empresas anteriores ao F14 (só de teste): CANCELADA com data antiga, já fora do prazo do expurgo (F14-08,
            // decisão 13 do G2). Só as linhas que a coluna nova recebeu vazias.
            migrationBuilder.Sql(ScriptsDeDados.CancelarEmpresasAnterioresAoF14);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cancelada_em",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "fim_do_teste",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "situacao",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "somente_leitura_desde",
                table: "tenants");
        }
    }
}
