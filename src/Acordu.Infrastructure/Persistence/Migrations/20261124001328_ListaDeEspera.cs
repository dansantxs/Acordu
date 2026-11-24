using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ListaDeEspera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "entrada_lista_espera_id",
                table: "convites_empresa",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "entradas_lista_espera",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    nome_empresa = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mensagem = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    convidada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entradas_lista_espera", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_convites_empresa_entrada_lista_espera_id",
                table: "convites_empresa",
                column: "entrada_lista_espera_id");

            migrationBuilder.CreateIndex(
                name: "ix_entradas_lista_espera_email",
                table: "entradas_lista_espera",
                column: "email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_convites_empresa_entrada_lista_espera_entrada_lista_espera_",
                table: "convites_empresa",
                column: "entrada_lista_espera_id",
                principalTable: "entradas_lista_espera",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_convites_empresa_entrada_lista_espera_entrada_lista_espera_",
                table: "convites_empresa");

            migrationBuilder.DropTable(
                name: "entradas_lista_espera");

            migrationBuilder.DropIndex(
                name: "ix_convites_empresa_entrada_lista_espera_id",
                table: "convites_empresa");

            migrationBuilder.DropColumn(
                name: "entrada_lista_espera_id",
                table: "convites_empresa");
        }
    }
}
