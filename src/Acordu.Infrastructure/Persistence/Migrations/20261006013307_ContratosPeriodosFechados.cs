using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContratosPeriodosFechados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<DateOnly>>(
                name: "periodos_fechados",
                table: "contratos",
                type: "date[]",
                nullable: false,
                // Acrescentado à mão: contratos já existentes nascem sem meses fechados.
                defaultValueSql: "'{}'::date[]");

            migrationBuilder.CreateIndex(
                name: "ix_contratos_tenant_id_cliente_id_tipo",
                table: "contratos",
                columns: new[] { "tenant_id", "cliente_id", "tipo" },
                filter: "status = 'ATIVO'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_contratos_tenant_id_cliente_id_tipo",
                table: "contratos");

            migrationBuilder.DropColumn(
                name: "periodos_fechados",
                table: "contratos");
        }
    }
}
