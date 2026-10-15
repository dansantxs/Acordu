using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssinaturasAgenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assinaturas_agenda",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    discreta = table.Column<bool>(type: "boolean", nullable: false),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assinaturas_agenda", x => new { x.tenant_id, x.usuario_id });
                });

            migrationBuilder.CreateIndex(
                name: "ix_assinaturas_agenda_token_hash",
                table: "assinaturas_agenda",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assinaturas_agenda");
        }
    }
}
