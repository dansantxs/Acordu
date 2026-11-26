using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConversasPorEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "conversas_por_email",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character(16)", fixedLength: true, maxLength: 16, nullable: false),
                    message_id_origem = table.Column<string>(type: "character varying(998)", maxLength: 998, nullable: true),
                    assunto_origem = table.Column<string>(type: "character varying(998)", maxLength: 998, nullable: true),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversas_por_email", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_conversas_por_email_tenant_id_cliente_id",
                table: "conversas_por_email",
                columns: new[] { "tenant_id", "cliente_id" });

            migrationBuilder.CreateIndex(
                name: "ix_conversas_por_email_tenant_id_solicitacao_id",
                table: "conversas_por_email",
                columns: new[] { "tenant_id", "solicitacao_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_conversas_por_email_tenant_id_token",
                table: "conversas_por_email",
                columns: new[] { "tenant_id", "token" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "conversas_por_email");
        }
    }
}
