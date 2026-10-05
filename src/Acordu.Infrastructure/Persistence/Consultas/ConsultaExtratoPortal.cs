using Acordu.Application.Portal.Extrato;
using Acordu.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Consultas;

/// <summary>
/// Extrato do portal (read side §4): apontamentos faturáveis do contrato no mês, por data e solicitação. No contexto de
/// contato, o SQL também filtra pelo cliente (defesa em profundidade além do filtro global do contrato).
/// </summary>
public sealed class ConsultaExtratoPortal : IConsultaExtratoPortal
{
    public const string DescricaoGenerica = "Atendimento";

    private readonly AcorduDbContext _db;
    private readonly ConsultaSql _sql;

    public ConsultaExtratoPortal(AcorduDbContext db, ConsultaSql sql)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(sql);
        _db = db;
        _sql = sql;
    }

    public async Task<ExtratoContratoDto?> ExtratoAsync(Guid contratoId, MesReferencia periodo, CancellationToken cancellationToken = default)
    {
        if (!await _db.Contratos.AnyAsync(c => c.Id == contratoId, cancellationToken))
            return null;

        var inicio = periodo.PrimeiroDia;
        var fim = periodo.UltimoDia;
        var linhas = await _sql.ListarAsync<LinhaBruta>(
            e => $"""
                SELECT a.data, a.duracao_minutos, a.descricao, a.descricao_externa, a.visivel_cliente,
                       s.numero AS solicitacao_numero, s.titulo AS solicitacao_titulo,
                       t.titulo AS tarefa_titulo, t.visivel_cliente AS tarefa_visivel
                FROM apontamentos a
                JOIN tarefas t ON t.id = a.tarefa_id AND t.tenant_id = a.tenant_id
                LEFT JOIN solicitacoes s ON s.id = t.solicitacao_id AND s.tenant_id = a.tenant_id
                WHERE a.tenant_id = {e.TenantId} AND a.contrato_id = {contratoId}
                  AND (CAST({e.ClienteId} AS uuid) IS NULL OR a.cliente_id = {e.ClienteId})
                  AND a.faturavel AND a.data BETWEEN {inicio} AND {fim}
                ORDER BY a.data, s.numero NULLS LAST, a.id
                """,
            cancellationToken);

        var dtos = linhas.Select(ParaLinha).ToList();
        return new ExtratoContratoDto(contratoId, periodo, dtos.Sum(l => l.DuracaoMinutos), dtos);
    }

    private static LinhaExtratoDto ParaLinha(LinhaBruta l)
    {
        if (!l.VisivelCliente)
            return new LinhaExtratoDto(l.Data, l.SolicitacaoNumero, null, DescricaoGenerica, l.DuracaoMinutos);

        var titulo = l.SolicitacaoTitulo ?? (l.TarefaVisivel ? l.TarefaTitulo : DescricaoGenerica);
        return new LinhaExtratoDto(l.Data, l.SolicitacaoNumero, titulo, l.DescricaoExterna ?? l.Descricao, l.DuracaoMinutos);
    }

    private sealed class LinhaBruta
    {
        public DateOnly Data { get; set; }

        public int DuracaoMinutos { get; set; }

        public string Descricao { get; set; } = "";

        public string? DescricaoExterna { get; set; }

        public bool VisivelCliente { get; set; }

        public int? SolicitacaoNumero { get; set; }

        public string? SolicitacaoTitulo { get; set; }

        public string TarefaTitulo { get; set; } = "";

        public bool TarefaVisivel { get; set; }
    }
}
