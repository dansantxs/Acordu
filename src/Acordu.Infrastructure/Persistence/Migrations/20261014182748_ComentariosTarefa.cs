using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ComentariosTarefa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_anexos",
                table: "anexos");

            migrationBuilder.AddPrimaryKey(
                name: "PK_anexos",
                table: "anexos",
                column: "id");

            migrationBuilder.CreateTable(
                name: "comentarios_tarefa",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarefa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    autor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    texto = table.Column<string>(type: "text", nullable: true),
                    registrado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comentarios_tarefa", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "anexos_comentario_tarefa",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_arquivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    tipo_conteudo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: false),
                    chave_armazenamento = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    comentario_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_anexos_comentario_tarefa", x => x.id);
                    table.ForeignKey(
                        name: "fk_anexos_comentario_tarefa_comentarios_tarefa_comentario_id",
                        column: x => x.comentario_id,
                        principalTable: "comentarios_tarefa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_anexos_comentario_tarefa_comentario_id",
                table: "anexos_comentario_tarefa",
                column: "comentario_id");

            migrationBuilder.CreateIndex(
                name: "ix_comentarios_tarefa_tenant_id_tarefa_id_registrado_em",
                table: "comentarios_tarefa",
                columns: new[] { "tenant_id", "tarefa_id", "registrado_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "anexos_comentario_tarefa");

            migrationBuilder.DropTable(
                name: "comentarios_tarefa");

            migrationBuilder.DropPrimaryKey(
                name: "PK_anexos",
                table: "anexos");

            migrationBuilder.AddPrimaryKey(
                name: "pk_anexos",
                table: "anexos",
                column: "id");
        }
    }
}
