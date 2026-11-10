using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OcorrenciasErro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ocorrencias_erro",
                columns: table => new
                {
                    impressao = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    servico = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    tipo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    quadro = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    rota = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    versao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    primeira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ultima_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    avisada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    por_resumo = table.Column<bool>(type: "boolean", nullable: false),
                    segurada = table.Column<bool>(type: "boolean", nullable: false),
                    total = table.Column<int>(type: "integer", nullable: false),
                    desde_aviso = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ocorrencias_erro", x => x.impressao);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ocorrencias_erro_avisada_em",
                table: "ocorrencias_erro",
                column: "avisada_em");

            migrationBuilder.CreateIndex(
                name: "ix_ocorrencias_erro_ultima_em",
                table: "ocorrencias_erro",
                column: "ultima_em");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ocorrencias_erro");
        }
    }
}
