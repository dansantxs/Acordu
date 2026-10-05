using Acordu.Application.Solicitacoes;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Consultas;

/// <summary>
/// Semelhantes pelo read side §8. Candidatas: resolvidas/fechadas com solução, que casam em full-text (termos em OU) ou
/// por trigrama no título. Relevância = 0,7 × rank normalizado + 0,3 × similaridade do título + 0,05 por mesma categoria
/// e + 0,05 por mesmo cliente (desempate, nunca filtro). Top 5 com relevância ≥ 0,15.
/// No contexto de contato, só solicitações do próprio cliente entram — solução de outro cliente nunca vaza pelo portal.
/// </summary>
public sealed class ConsultaSemelhantes : IConsultaSemelhantes
{
    public const int Maximo = 5;
    public const double RelevanciaMinima = 0.15;

    private readonly AcorduDbContext _db;
    private readonly ConsultaSql _sql;

    public ConsultaSemelhantes(AcorduDbContext db, ConsultaSql sql)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(sql);
        _db = db;
        _sql = sql;
    }

    public async Task<IReadOnlyList<SolicitacaoSemelhanteDto>?> SemelhantesAsync(
        Guid solicitacaoId, CancellationToken cancellationToken = default)
    {
        if (!await _db.Solicitacoes.AnyAsync(s => s.Id == solicitacaoId, cancellationToken))
            return null;

        var linhas = await _sql.ListarAsync<Linha>(e => $"""
            WITH alvo AS (
                SELECT id, titulo, categoria_id, cliente_id,
                       replace(plainto_tsquery('portuguese_unaccent', titulo || ' ' || coalesce(descricao, ''))::text, ' & ', ' | ')::tsquery AS consulta
                FROM solicitacoes
                WHERE tenant_id = {e.TenantId} AND id = {solicitacaoId}
                  AND (CAST({e.ClienteId} AS uuid) IS NULL OR cliente_id = {e.ClienteId})
            ),
            candidatas AS (
                SELECT s.id, s.numero, s.titulo, left(s.solucao_texto, 200) AS solucao_resumo, s.resolvida_em,
                       (0.7 * ts_rank_cd(s.busca_tsv, a.consulta, 32)
                        + 0.3 * similarity(s.titulo, a.titulo)
                        + CASE WHEN s.categoria_id IS NOT NULL AND s.categoria_id = a.categoria_id THEN 0.05 ELSE 0 END
                        + CASE WHEN s.cliente_id = a.cliente_id THEN 0.05 ELSE 0 END)::float8 AS relevancia
                FROM solicitacoes s
                CROSS JOIN alvo a
                WHERE s.tenant_id = {e.TenantId}
                  AND (CAST({e.ClienteId} AS uuid) IS NULL OR s.cliente_id = {e.ClienteId})
                  AND s.id <> a.id
                  AND s.status IN ('RESOLVIDA', 'FECHADA')
                  AND s.solucao_texto IS NOT NULL
                  AND (s.busca_tsv @@ a.consulta OR s.titulo % a.titulo)
            )
            SELECT id, numero, titulo, solucao_resumo, resolvida_em, relevancia
            FROM candidatas
            WHERE relevancia >= {RelevanciaMinima}
            ORDER BY relevancia DESC, numero DESC
            LIMIT {Maximo}
            """, cancellationToken);

        return linhas
            .Select(l => new SolicitacaoSemelhanteDto(
                l.Id, l.Numero, l.Titulo, l.SolucaoResumo, l.ResolvidaEm, Math.Round(l.Relevancia, 2)))
            .ToList();
    }

    private sealed class Linha
    {
        public Guid Id { get; set; }

        public int Numero { get; set; }

        public string Titulo { get; set; } = "";

        public string SolucaoResumo { get; set; } = "";

        public DateTimeOffset ResolvidaEm { get; set; }

        public double Relevancia { get; set; }
    }
}
