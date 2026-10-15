using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Atividades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "atividades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequencia = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chave = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ocorrida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    autor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    autor_eh_cliente = table.Column<bool>(type: "boolean", nullable: false),
                    alvo = table.Column<int>(type: "integer", maxLength: 40, nullable: false),
                    alvo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status_de = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    status_para = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    responsavel_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_atividades", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_atividades_ocorrida_em",
                table: "atividades",
                column: "ocorrida_em");

            migrationBuilder.CreateIndex(
                name: "ix_atividades_tenant_id_chave",
                table: "atividades",
                columns: new[] { "tenant_id", "chave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_atividades_tenant_id_cliente_id_sequencia",
                table: "atividades",
                columns: new[] { "tenant_id", "cliente_id", "sequencia" });

            migrationBuilder.CreateIndex(
                name: "ix_atividades_tenant_id_sequencia",
                table: "atividades",
                columns: new[] { "tenant_id", "sequencia" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "atividades");
        }
    }
}
