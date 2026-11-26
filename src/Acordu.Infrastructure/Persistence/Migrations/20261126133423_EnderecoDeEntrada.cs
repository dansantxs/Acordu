using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnderecoDeEntrada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "codigo_entrada",
                table: "tenants",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "recebimento_email",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_tenants_codigo_entrada",
                table: "tenants",
                column: "codigo_entrada",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tenants_codigo_entrada",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "codigo_entrada",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "recebimento_email",
                table: "tenants");
        }
    }
}
