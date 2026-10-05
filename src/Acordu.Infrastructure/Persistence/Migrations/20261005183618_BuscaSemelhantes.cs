using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BuscaSemelhantes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "busca_tsv",
                table: "solicitacoes",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "setweight(to_tsvector('portuguese_unaccent', coalesce(titulo, '')), 'A') || setweight(to_tsvector('portuguese_unaccent', coalesce(solucao_problema, '') || ' ' || coalesce(descricao, '')), 'B') || setweight(to_tsvector('portuguese_unaccent', coalesce(solucao_texto, '')), 'C')",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "ix_solicitacoes_busca_tsv",
                table: "solicitacoes",
                column: "busca_tsv")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_solicitacoes_titulo",
                table: "solicitacoes",
                column: "titulo")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_solicitacoes_busca_tsv",
                table: "solicitacoes");

            migrationBuilder.DropIndex(
                name: "ix_solicitacoes_titulo",
                table: "solicitacoes");

            migrationBuilder.DropColumn(
                name: "busca_tsv",
                table: "solicitacoes");
        }
    }
}
