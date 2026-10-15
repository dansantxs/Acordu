using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Seguidores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "autor_id",
                table: "outbox_mensagens",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "seguidores",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alvo = table.Column<int>(type: "integer", maxLength: 40, nullable: false),
                    alvo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    desde = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seguidores", x => new { x.tenant_id, x.alvo, x.alvo_id, x.usuario_id });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "seguidores");

            migrationBuilder.DropColumn(
                name: "autor_id",
                table: "outbox_mensagens");
        }
    }
}
