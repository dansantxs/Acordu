using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "acessos_portal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    emitido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revogado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acessos_portal", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ajustes_saldo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantidade_minutos = table.Column<int>(type: "integer", nullable: false),
                    motivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    autor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ajustes_saldo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "apontamentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarefa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: true),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    duracao_minutos = table.Column<int>(type: "integer", nullable: false),
                    descricao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    descricao_externa = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    faturavel = table.Column<bool>(type: "boolean", nullable: false),
                    visivel_cliente = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_apontamentos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "calendarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fuso_horario = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    feriados = table.Column<DateOnly[]>(type: "date[]", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendarios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categorias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ativa = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categorias", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "clientes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    documento = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    observacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clientes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contratos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    vigencia_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    vigencia_fim = table.Column<DateOnly>(type: "date", nullable: true),
                    franquia_minutos = table.Column<int>(type: "integer", nullable: true),
                    sla = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contratos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cronometros",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarefa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    iniciado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cronometros", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "interacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    autor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    autor_eh_cliente = table.Column<bool>(type: "boolean", nullable: false),
                    texto = table.Column<string>(type: "text", nullable: true),
                    visibilidade = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    registrada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_interacoes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "projetos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: true),
                    prazo = table.Column<DateOnly>(type: "date", nullable: true),
                    estimativa_minutos = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_projetos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "recorrencias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modelo = table.Column<string>(type: "jsonb", nullable: false),
                    regra_periodicidade = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    regra_dia_referencia = table.Column<int>(type: "integer", nullable: false),
                    regra_antecedencia_dias = table.Column<int>(type: "integer", nullable: false),
                    regra_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    regra_fim = table.Column<DateOnly>(type: "date", nullable: true),
                    proxima_data = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recorrencias", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "solicitacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: true),
                    prioridade = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    origem = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    contato_id = table.Column<Guid>(type: "uuid", nullable: true),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: true),
                    categoria_id = table.Column<Guid>(type: "uuid", nullable: true),
                    responsavel_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sla_aberto_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sla_inicio_ciclo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sla_tempo_primeira_resposta_minutos = table.Column<int>(type: "integer", nullable: true),
                    sla_tempo_resolucao_minutos = table.Column<int>(type: "integer", nullable: true),
                    sla_pausado_no_ciclo_minutos = table.Column<int>(type: "integer", nullable: true),
                    sla_pausado_desde = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    prazo_primeira_resposta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    prazo_resolucao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    limite_risco = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    solucao_texto = table.Column<string>(type: "text", nullable: true),
                    solucao_problema = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    solucao_causa = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    aberta_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    primeira_resposta_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolvida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fechada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    motivo_cancelamento = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_solicitacoes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tarefas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    responsavel_id = table.Column<Guid>(type: "uuid", nullable: true),
                    prazo = table.Column<DateOnly>(type: "date", nullable: true),
                    estimativa_minutos = table.Column<int>(type: "integer", nullable: true),
                    solicitacao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    projeto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recorrencia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    data_referencia = table.Column<DateOnly>(type: "date", nullable: true),
                    visivel_cliente = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tarefas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "calendario_expediente",
                columns: table => new
                {
                    dia = table.Column<int>(type: "integer", nullable: false),
                    calendario_util_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inicio = table.Column<TimeOnly>(type: "time", nullable: false),
                    fim = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendario_expediente", x => new { x.calendario_util_id, x.dia });
                    table.ForeignKey(
                        name: "fk_calendario_expediente_calendarios_calendario_util_id",
                        column: x => x.calendario_util_id,
                        principalTable: "calendarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contatos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    telefone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    cargo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contatos", x => x.id);
                    table.ForeignKey(
                        name: "fk_contatos_clientes_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "clientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "anexos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_arquivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    tipo_conteudo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: false),
                    chave_armazenamento = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    interacao_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_anexos", x => x.id);
                    table.ForeignKey(
                        name: "fk_anexos_interacoes_interacao_id",
                        column: x => x.interacao_id,
                        principalTable: "interacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "itens_checklist",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    texto = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    feito = table.Column<bool>(type: "boolean", nullable: false),
                    tarefa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_itens_checklist", x => x.id);
                    table.ForeignKey(
                        name: "fk_itens_checklist_tarefas_tarefa_id",
                        column: x => x.tarefa_id,
                        principalTable: "tarefas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_acessos_portal_tenant_id_cliente_id",
                table: "acessos_portal",
                columns: new[] { "tenant_id", "cliente_id" });

            migrationBuilder.CreateIndex(
                name: "ix_acessos_portal_tenant_id_solicitacao_id_contato_id",
                table: "acessos_portal",
                columns: new[] { "tenant_id", "solicitacao_id", "contato_id" });

            migrationBuilder.CreateIndex(
                name: "ix_acessos_portal_token_hash",
                table: "acessos_portal",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ajustes_saldo_tenant_id_cliente_id",
                table: "ajustes_saldo",
                columns: new[] { "tenant_id", "cliente_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ajustes_saldo_tenant_id_contrato_id_data",
                table: "ajustes_saldo",
                columns: new[] { "tenant_id", "contrato_id", "data" });

            migrationBuilder.CreateIndex(
                name: "ix_anexos_interacao_id",
                table: "anexos",
                column: "interacao_id");

            migrationBuilder.CreateIndex(
                name: "ix_apontamentos_tenant_id_cliente_id_data",
                table: "apontamentos",
                columns: new[] { "tenant_id", "cliente_id", "data" });

            migrationBuilder.CreateIndex(
                name: "ix_apontamentos_tenant_id_contrato_id_data",
                table: "apontamentos",
                columns: new[] { "tenant_id", "contrato_id", "data" });

            migrationBuilder.CreateIndex(
                name: "ix_apontamentos_tenant_id_tarefa_id",
                table: "apontamentos",
                columns: new[] { "tenant_id", "tarefa_id" });

            migrationBuilder.CreateIndex(
                name: "ix_calendarios_tenant_id",
                table: "calendarios",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_categorias_tenant_id_nome",
                table: "categorias",
                columns: new[] { "tenant_id", "nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_clientes_tenant_id_nome",
                table: "clientes",
                columns: new[] { "tenant_id", "nome" });

            migrationBuilder.CreateIndex(
                name: "ix_contatos_cliente_id_email",
                table: "contatos",
                columns: new[] { "cliente_id", "email" },
                unique: true,
                filter: "ativo");

            migrationBuilder.CreateIndex(
                name: "ix_contatos_usuario_id",
                table: "contatos",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_contratos_tenant_id_cliente_id_status",
                table: "contratos",
                columns: new[] { "tenant_id", "cliente_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_cronometros_tenant_id_usuario_id",
                table: "cronometros",
                columns: new[] { "tenant_id", "usuario_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_interacoes_tenant_id_cliente_id",
                table: "interacoes",
                columns: new[] { "tenant_id", "cliente_id" });

            migrationBuilder.CreateIndex(
                name: "ix_interacoes_tenant_id_solicitacao_id_registrada_em",
                table: "interacoes",
                columns: new[] { "tenant_id", "solicitacao_id", "registrada_em" });

            migrationBuilder.CreateIndex(
                name: "ix_itens_checklist_tarefa_id",
                table: "itens_checklist",
                column: "tarefa_id");

            migrationBuilder.CreateIndex(
                name: "ix_projetos_tenant_id_cliente_id",
                table: "projetos",
                columns: new[] { "tenant_id", "cliente_id" });

            migrationBuilder.CreateIndex(
                name: "ix_recorrencias_status_proxima_data",
                table: "recorrencias",
                columns: new[] { "status", "proxima_data" });

            migrationBuilder.CreateIndex(
                name: "ix_recorrencias_tenant_id_cliente_id",
                table: "recorrencias",
                columns: new[] { "tenant_id", "cliente_id" });

            migrationBuilder.CreateIndex(
                name: "ix_solicitacoes_tenant_id_cliente_id_status",
                table: "solicitacoes",
                columns: new[] { "tenant_id", "cliente_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_solicitacoes_tenant_id_numero",
                table: "solicitacoes",
                columns: new[] { "tenant_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tarefas_recorrencia_id_data_referencia",
                table: "tarefas",
                columns: new[] { "recorrencia_id", "data_referencia" },
                unique: true,
                filter: "recorrencia_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_tarefas_tenant_id_cliente_id_status",
                table: "tarefas",
                columns: new[] { "tenant_id", "cliente_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_tarefas_tenant_id_projeto_id",
                table: "tarefas",
                columns: new[] { "tenant_id", "projeto_id" });

            migrationBuilder.CreateIndex(
                name: "ix_tarefas_tenant_id_solicitacao_id",
                table: "tarefas",
                columns: new[] { "tenant_id", "solicitacao_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "acessos_portal");

            migrationBuilder.DropTable(
                name: "ajustes_saldo");

            migrationBuilder.DropTable(
                name: "anexos");

            migrationBuilder.DropTable(
                name: "apontamentos");

            migrationBuilder.DropTable(
                name: "calendario_expediente");

            migrationBuilder.DropTable(
                name: "categorias");

            migrationBuilder.DropTable(
                name: "contatos");

            migrationBuilder.DropTable(
                name: "contratos");

            migrationBuilder.DropTable(
                name: "cronometros");

            migrationBuilder.DropTable(
                name: "itens_checklist");

            migrationBuilder.DropTable(
                name: "projetos");

            migrationBuilder.DropTable(
                name: "recorrencias");

            migrationBuilder.DropTable(
                name: "solicitacoes");

            migrationBuilder.DropTable(
                name: "interacoes");

            migrationBuilder.DropTable(
                name: "calendarios");

            migrationBuilder.DropTable(
                name: "clientes");

            migrationBuilder.DropTable(
                name: "tarefas");
        }
    }
}
