using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmailsRecebidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "emails_recebidos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chave_entrada = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    remetente_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    recebido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resultado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_emails_recebidos", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_emails_recebidos_tenant_id_chave_entrada",
                table: "emails_recebidos",
                columns: new[] { "tenant_id", "chave_entrada" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "emails_recebidos");
        }
    }
}
