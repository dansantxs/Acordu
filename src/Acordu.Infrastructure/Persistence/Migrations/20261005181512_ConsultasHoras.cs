using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConsultasHoras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_apontamentos_tenant_id_contrato_id_data",
                table: "apontamentos");

            migrationBuilder.CreateIndex(
                name: "ix_tarefas_tenant_id_responsavel_id_status",
                table: "tarefas",
                columns: new[] { "tenant_id", "responsavel_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_apontamentos_tenant_id_contrato_id_data",
                table: "apontamentos",
                columns: new[] { "tenant_id", "contrato_id", "data" })
                .Annotation("Npgsql:IndexInclude", new[] { "duracao_minutos", "faturavel" });

            migrationBuilder.CreateIndex(
                name: "ix_apontamentos_tenant_id_usuario_id_data",
                table: "apontamentos",
                columns: new[] { "tenant_id", "usuario_id", "data" });

            // Acrescentado à mão (convenções §1): o EF não indexa colunas de tipo owned (RelogioSla) junto das da raiz.
            // Filtro e ordenação da fila por situação de SLA (read side §5).
            migrationBuilder.Sql("CREATE INDEX ix_solicitacoes_tenant_id_status_prazo_resolucao ON solicitacoes (tenant_id, status, prazo_resolucao);");
            migrationBuilder.Sql("CREATE INDEX ix_solicitacoes_tenant_id_status_limite_risco ON solicitacoes (tenant_id, status, limite_risco);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tarefas_tenant_id_responsavel_id_status",
                table: "tarefas");

            migrationBuilder.DropIndex(
                name: "ix_apontamentos_tenant_id_contrato_id_data",
                table: "apontamentos");

            migrationBuilder.DropIndex(
                name: "ix_apontamentos_tenant_id_usuario_id_data",
                table: "apontamentos");

            migrationBuilder.CreateIndex(
                name: "ix_apontamentos_tenant_id_contrato_id_data",
                table: "apontamentos",
                columns: new[] { "tenant_id", "contrato_id", "data" });

            migrationBuilder.Sql("DROP INDEX ix_solicitacoes_tenant_id_status_prazo_resolucao;");
            migrationBuilder.Sql("DROP INDEX ix_solicitacoes_tenant_id_status_limite_risco;");
        }
    }
}
