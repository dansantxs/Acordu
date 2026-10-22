using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfiguracaoEmpresa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "responder_para",
                table: "tenants",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sla_padrao",
                table: "tenants",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "responder_para",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "sla_padrao",
                table: "tenants");
        }
    }
}
