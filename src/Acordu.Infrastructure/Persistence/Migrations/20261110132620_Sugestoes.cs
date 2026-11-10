using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sugestoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sugestoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pessoa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    texto = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    tela = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    avisada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sugestoes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sugestoes_empresa_id",
                table: "sugestoes",
                column: "empresa_id");

            migrationBuilder.CreateIndex(
                name: "ix_sugestoes_numero",
                table: "sugestoes",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sugestoes_numero1",
                table: "sugestoes",
                column: "numero",
                filter: "avisada_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sugestoes_pessoa_id_criada_em",
                table: "sugestoes",
                columns: new[] { "pessoa_id", "criada_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sugestoes");
        }
    }
}
