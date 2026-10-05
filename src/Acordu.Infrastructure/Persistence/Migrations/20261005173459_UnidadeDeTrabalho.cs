using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UnidadeDeTrabalho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "contadores",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    valor = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contadores", x => new { x.tenant_id, x.nome });
                });

            migrationBuilder.CreateTable(
                name: "outbox_mensagens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequencia = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    conteudo = table.Column<string>(type: "jsonb", nullable: false),
                    ocorrida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tentativas = table.Column<int>(type: "integer", nullable: false),
                    proxima_tentativa_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    falhou_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    erro = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_mensagens", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_mensagens_proxima_tentativa_em",
                table: "outbox_mensagens",
                column: "proxima_tentativa_em",
                filter: "processada_em IS NULL AND falhou_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_mensagens_sequencia",
                table: "outbox_mensagens",
                column: "sequencia",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contadores");

            migrationBuilder.DropTable(
                name: "outbox_mensagens");
        }
    }
}
