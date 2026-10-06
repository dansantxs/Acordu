using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConvitesPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "convites_portal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    escopo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    token_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    aceito_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revogado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_convites_portal", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_convites_portal_tenant_id_cliente_id_contato_id",
                table: "convites_portal",
                columns: new[] { "tenant_id", "cliente_id", "contato_id" });

            migrationBuilder.CreateIndex(
                name: "ix_convites_portal_token_hash",
                table: "convites_portal",
                column: "token_hash",
                unique: true,
                filter: "token_hash IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "convites_portal");
        }
    }
}
