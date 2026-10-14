using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Planejamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "capacidade_semanal_minutos",
                table: "vinculos",
                type: "integer",
                nullable: true);

            // Quem já está na equipe ganha a capacidade padrão (40h); vínculo de cliente fica nulo.
            migrationBuilder.Sql("UPDATE vinculos SET capacidade_semanal_minutos = 2400 WHERE cliente_id IS NULL;");

            migrationBuilder.AddColumn<DateOnly>(
                name: "planejada_para",
                table: "tarefas",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "inicio",
                table: "projetos",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "capacidade_semanal_minutos",
                table: "vinculos");

            migrationBuilder.DropColumn(
                name: "planejada_para",
                table: "tarefas");

            migrationBuilder.DropColumn(
                name: "inicio",
                table: "projetos");
        }
    }
}
