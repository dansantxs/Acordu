using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CanalDasMensagens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "canal",
                table: "interacoes",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            // Mensagens anteriores ao F15 (F15-62): do contato → PORTAL, da equipe → ACORDU. Só as linhas que a
            // coluna nova recebeu vazias.
            migrationBuilder.Sql(ScriptsDeDados.PreencherCanalDasInteracoes);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "canal",
                table: "interacoes");
        }
    }
}
