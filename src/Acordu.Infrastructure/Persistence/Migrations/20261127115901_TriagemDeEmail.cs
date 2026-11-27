using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TriagemDeEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "item_triagem_id",
                table: "emails_recebidos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "itens_triagem_email",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    remetente_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    remetente_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    assunto = table.Column<string>(type: "character varying(998)", maxLength: 998, nullable: true),
                    texto = table.Column<string>(type: "text", nullable: true),
                    recebido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    motivo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    clientes_possiveis = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    recusados = table.Column<string>(type: "jsonb", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_itens_triagem_email", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "anexos_triagem_email",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_arquivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    tipo_conteudo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: false),
                    chave_armazenamento = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    item_triagem_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_anexos_triagem_email", x => x.id);
                    table.ForeignKey(
                        name: "fk_anexos_triagem_email_itens_triagem_email_item_triagem_id",
                        column: x => x.item_triagem_id,
                        principalTable: "itens_triagem_email",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_anexos_triagem_email_item_triagem_id",
                table: "anexos_triagem_email",
                column: "item_triagem_id");

            migrationBuilder.CreateIndex(
                name: "ix_itens_triagem_email_tenant_id_recebido_em",
                table: "itens_triagem_email",
                columns: new[] { "tenant_id", "recebido_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "anexos_triagem_email");

            migrationBuilder.DropTable(
                name: "itens_triagem_email");

            migrationBuilder.DropColumn(
                name: "item_triagem_id",
                table: "emails_recebidos");
        }
    }
}
