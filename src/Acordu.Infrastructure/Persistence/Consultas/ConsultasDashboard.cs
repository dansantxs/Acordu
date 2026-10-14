using Acordu.Application.Contratos;
using Acordu.Application.Dashboard;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Consultas;

/// <summary>
/// Blocos do dashboard pelos filtros globais (read side §6): contagens e somas no banco, uma consulta por bloco. O consumo
/// de contrato reaproveita as fórmulas do §3 (<see cref="IConsultasContrato"/>), um contrato por vez.
/// </summary>
public sealed class ConsultasDashboard : IConsultasDashboard
{
    private static readonly StatusSolicitacao[] EmAberto =
    [
        StatusSolicitacao.Aberta, StatusSolicitacao.EmAndamento, StatusSolicitacao.AguardandoCliente, StatusSolicitacao.AguardandoTerceiro,
    ];

    private readonly AcorduDbContext _db;
    private readonly ConsultaSql _sql;
    private readonly IConsultasContrato _contratos;

    public ConsultasDashboard(AcorduDbContext db, ConsultaSql sql, IConsultasContrato contratos)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(contratos);
        _db = db;
        _sql = sql;
        _contratos = contratos;
    }

    public async Task<FilaDto> FilaAsync(CancellationToken cancellationToken = default)
    {
        var porStatus = await _db.Solicitacoes.AsNoTracking()
            .Where(s => EmAberto.Contains(s.Status))
            .GroupBy(s => s.Status)
            .Select(g => new { Status = g.Key, Total = g.Count(), SemResponsavel = g.Count(s => s.ResponsavelId == null) })
            .ToListAsync(cancellationToken);
        int Total(StatusSolicitacao status) => porStatus.Where(x => x.Status == status).Sum(x => x.Total);
        return new FilaDto(
            Total(StatusSolicitacao.Aberta),
            Total(StatusSolicitacao.EmAndamento),
            Total(StatusSolicitacao.AguardandoCliente),
            Total(StatusSolicitacao.AguardandoTerceiro),
            porStatus.Sum(x => x.SemResponsavel));
    }

    /// <summary>Mesmas regras do filtro <c>situacaoSla</c> da lista (read side §5), só para as em aberto e não pausadas.</summary>
    public async Task<SlaDashboardDto> SlaAsync(
        DateTimeOffset agora, DateTimeOffset desde, DateTimeOffset antes, CancellationToken cancellationToken = default)
    {
        var abertas = _db.Solicitacoes.AsNoTracking()
            .Where(s => s.Sla != null && EmAberto.Contains(s.Status) && s.Sla.PausadoDesde == null);
        var violadas = await abertas.CountAsync(
            s => agora > s.Sla!.PrazoResolucao || (s.PrimeiraRespostaEm == null && agora > s.Sla.PrazoPrimeiraResposta),
            cancellationToken);
        var emRisco = await abertas.CountAsync(
            s => s.Sla!.LimiteRisco <= agora && agora <= s.Sla.PrazoResolucao
                && !(s.PrimeiraRespostaEm == null && agora > s.Sla.PrazoPrimeiraResposta),
            cancellationToken);

        var resolvidas = _db.Solicitacoes.AsNoTracking()
            .Where(s => s.Sla != null && s.ResolvidaEm != null && s.ResolvidaEm >= desde && s.ResolvidaEm < antes);
        var total = await resolvidas.CountAsync(cancellationToken);
        var noPrazo = total == 0 ? 0 : await resolvidas.CountAsync(s => s.ResolvidaEm <= s.Sla!.PrazoResolucao, cancellationToken);
        int? cumprimento = total > 0 ? (int)(noPrazo * 100L / total) : null;
        return new SlaDashboardDto(emRisco, violadas, cumprimento);
    }

    /// <summary>Somas em SQL (a duração é coluna convertida, sem soma traduzível pelo LINQ), como o consumo do §3.</summary>
    public async Task<HorasDashboardDto> HorasAsync(DateOnly de, DateOnly ate, int clientes, CancellationToken cancellationToken = default)
    {
        var somas = (await _sql.ListarAsync<SomaHoras>(e => $"""
            SELECT
                coalesce(sum(duracao_minutos) FILTER (WHERE faturavel), 0)::int AS faturavel,
                coalesce(sum(duracao_minutos) FILTER (WHERE NOT faturavel), 0)::int AS nao_faturavel
            FROM apontamentos
            WHERE tenant_id = {e.TenantId} AND data BETWEEN {de} AND {ate}
              AND (CAST({e.ClienteId} AS uuid) IS NULL OR cliente_id = {e.ClienteId})
            """, cancellationToken)).Single();
        var porCliente = await _sql.ListarAsync<HorasCliente>(e => $"""
            SELECT a.cliente_id, c.nome, sum(a.duracao_minutos)::int AS minutos
            FROM apontamentos a
            JOIN clientes c ON c.id = a.cliente_id AND c.tenant_id = a.tenant_id
            WHERE a.tenant_id = {e.TenantId} AND a.faturavel AND a.data BETWEEN {de} AND {ate}
              AND (CAST({e.ClienteId} AS uuid) IS NULL OR a.cliente_id = {e.ClienteId})
            GROUP BY a.cliente_id, c.nome
            ORDER BY minutos DESC, a.cliente_id
            LIMIT {clientes}
            """, cancellationToken);
        return new HorasDashboardDto(
            somas.Faturavel,
            somas.NaoFaturavel,
            [.. porCliente.Select(x => new HorasClienteDto(x.ClienteId, x.Nome, x.Minutos))]);
    }

    /// <summary>Contratos ATIVOS vigentes hoje, com o consumo do mês corrente; mais consumidos primeiro (sem percentual por último).</summary>
    public async Task<IReadOnlyList<ContratoDashboardDto>> ContratosAsync(DateOnly hoje, CancellationToken cancellationToken = default)
    {
        var ativos = await (from c in _db.Contratos.AsNoTracking()
                            join cl in _db.Clientes on c.ClienteId equals cl.Id
                            where c.Status == StatusContrato.Ativo
                                && c.Vigencia.Inicio <= hoje && (c.Vigencia.Fim == null || c.Vigencia.Fim >= hoje)
                            select new { c.Id, Cliente = cl.Nome })
            .ToListAsync(cancellationToken);

        var periodo = MesReferencia.De(hoje);
        var linhas = new List<ContratoDashboardDto>(ativos.Count);
        foreach (var ativo in ativos)
        {
            if (await _contratos.ConsumoAsync(ativo.Id, periodo, hoje, cancellationToken) is not { } consumo)
                continue;
            var consumido = consumo.Tipo == TipoContrato.BancoHoras
                ? consumo.ConsumidoAcumuladoMinutos ?? consumo.ConsumidoMinutos
                : consumo.ConsumidoMinutos;
            linhas.Add(new ContratoDashboardDto(ativo.Id, ativo.Cliente, consumo.Tipo, consumo.FranquiaMinutos, consumido, consumo.Percentual));
        }

        return [.. linhas.OrderByDescending(l => l.Percentual.HasValue).ThenByDescending(l => l.Percentual).ThenBy(l => l.Cliente, StringComparer.Ordinal)];
    }

    public Task<int> TarefasVencidasAsync(DateOnly hoje, CancellationToken cancellationToken = default) =>
        _db.Tarefas.AsNoTracking().CountAsync(
            t => (t.Status == StatusTarefa.AFazer || t.Status == StatusTarefa.Fazendo) && t.Prazo != null && t.Prazo < hoje,
            cancellationToken);

    /// <summary>O modelo é jsonb com conversor: o título sai em memória, depois do limite.</summary>
    public async Task<IReadOnlyList<ProximaRecorrenteDto>> ProximasRecorrentesAsync(int quantidade, CancellationToken cancellationToken = default)
    {
        var linhas = await (from r in _db.Recorrencias.AsNoTracking()
                            join cl in _db.Clientes on r.ClienteId equals cl.Id
                            where r.Status == StatusRecorrencia.Ativa && r.ProximaData != null
                            orderby r.ProximaData, r.Id
                            select new { r, Cliente = cl.Nome })
            .Take(quantidade)
            .ToListAsync(cancellationToken);
        return [.. linhas.Select(x => new ProximaRecorrenteDto(x.r.Id, x.r.Modelo.Titulo, x.Cliente, x.r.ProximaData!.Value))];
    }

    private sealed class SomaHoras
    {
        public int Faturavel { get; set; }

        public int NaoFaturavel { get; set; }
    }

    private sealed class HorasCliente
    {
        public Guid ClienteId { get; set; }

        public string Nome { get; set; } = string.Empty;

        public int Minutos { get; set; }
    }
}
