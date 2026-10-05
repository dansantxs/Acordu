using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtensoesBusca : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:unaccent", ",,");

            // Acrescentado à mão (convenções §1): unaccent() não é IMMUTABLE e não pode ir numa coluna gerada; uma
            // configuração de busca com o dicionário unaccent pode (to_tsvector com regconfig fixo é imutável).
            migrationBuilder.Sql("CREATE TEXT SEARCH CONFIGURATION portuguese_unaccent (COPY = portuguese);");
            migrationBuilder.Sql(
                "ALTER TEXT SEARCH CONFIGURATION portuguese_unaccent " +
                "ALTER MAPPING FOR hword, hword_part, word WITH unaccent, portuguese_stem;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Desfeito antes das extensões: a configuração depende do dicionário unaccent.
            migrationBuilder.Sql("DROP TEXT SEARCH CONFIGURATION portuguese_unaccent;");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:unaccent", ",,");
        }
    }
}
