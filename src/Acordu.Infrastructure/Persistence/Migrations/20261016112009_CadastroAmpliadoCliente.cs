using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CadastroAmpliadoCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "clientes",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_bairro",
                table: "clientes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cep",
                table: "clientes",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cidade",
                table: "clientes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_complemento",
                table: "clientes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_logradouro",
                table: "clientes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_numero",
                table: "clientes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_uf",
                table: "clientes",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "etiquetas",
                table: "clientes",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "inscricao_estadual",
                table: "clientes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "razao_social",
                table: "clientes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "responsavel_id",
                table: "clientes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "telefone",
                table: "clientes",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_clientes_tenant_id_responsavel_id",
                table: "clientes",
                columns: new[] { "tenant_id", "responsavel_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_clientes_tenant_id_responsavel_id",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "email",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "endereco_bairro",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "endereco_cep",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "endereco_cidade",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "endereco_complemento",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "endereco_logradouro",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "endereco_numero",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "endereco_uf",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "etiquetas",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "inscricao_estadual",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "razao_social",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "responsavel_id",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "telefone",
                table: "clientes");
        }
    }
}
