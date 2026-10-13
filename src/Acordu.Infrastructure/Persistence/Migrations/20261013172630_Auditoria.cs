using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Auditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auditoria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    acao = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    alvo_tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    alvo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dados = table.Column<string>(type: "jsonb", nullable: true),
                    ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    ocorrido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auditoria", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_ocorrido_em",
                table: "auditoria",
                column: "ocorrido_em");

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_tenant_id_ocorrido_em",
                table: "auditoria",
                columns: new[] { "tenant_id", "ocorrido_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auditoria");
        }
    }
}
