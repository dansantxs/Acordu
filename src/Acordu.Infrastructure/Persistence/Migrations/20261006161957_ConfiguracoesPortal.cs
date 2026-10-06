using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfiguracoesPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "dias_fechamento_automatico",
                table: "tenants",
                type: "integer",
                nullable: false,
                // Ajustado à mão: tenants já existentes ficam com o padrão do domínio (Tenant.DiasFechamentoPadrao).
                defaultValue: 5);

            migrationBuilder.AddColumn<string>(
                name: "nome_portal",
                table: "tenants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "dias_fechamento_automatico",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "nome_portal",
                table: "tenants");
        }
    }
}
