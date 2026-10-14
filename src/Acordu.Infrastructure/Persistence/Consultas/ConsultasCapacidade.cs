using Acordu.Application.Capacidade;
using Acordu.Application.Common;
using Acordu.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Consultas;

/// <summary>Linhas da capacidade pelos filtros globais (read side §6b). Durações são colunas convertidas: saem em memória.</summary>
public sealed class ConsultasCapacidade : IConsultasCapacidade
{
    private static readonly StatusSolicitacao[] EmAberto =
    [
        StatusSolicitacao.Aberta, StatusSolicitacao.EmAndamento, StatusSolicitacao.AguardandoCliente, StatusSolicitacao.AguardandoTerceiro,
    ];

    private readonly AcorduDbContext _db;

    public ConsultasCapacidade(AcorduDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<IReadOnlyList<MembroCapacidade>> EquipeAsync(CancellationToken cancellationToken = default)
    {
        var linhas = await (from v in _db.Vinculos.AsNoTracking()
                            join u in _db.Users on v.UsuarioId equals u.Id
                            where v.ClienteId == null
                            orderby u.Nome, u.Id
                            select new { u.Id, u.Nome, v.CapacidadeSemanal })
            .ToListAsync(cancellationToken);
        return [.. linhas.Select(l => new MembroCapacidade(new Referencia(l.Id, l.Nome), l.CapacidadeSemanal))];
    }

    public async Task<IReadOnlyList<TarefaCapacidade>> TarefasAsync(DateOnly de, DateOnly ate, CancellationToken cancellationToken = default)
    {
        var linhas = await (from t in _db.Tarefas.AsNoTracking()
                            let dia = t.PlanejadaPara ?? t.Prazo
                            where (t.Status == StatusTarefa.AFazer || t.Status == StatusTarefa.Fazendo) && dia >= de && dia <= ate
                            select new { t.ResponsavelId, Dia = dia!.Value, t.Estimativa })
            .ToListAsync(cancellationToken);
        return [.. linhas.Select(l => new TarefaCapacidade(l.ResponsavelId, l.Dia, l.Estimativa?.Minutos))];
    }

    public async Task<IReadOnlyList<Guid?>> SolicitacoesAsync(DateTimeOffset desde, DateTimeOffset antes, CancellationToken cancellationToken = default) =>
        await _db.Solicitacoes.AsNoTracking()
            .Where(s => s.Sla != null && EmAberto.Contains(s.Status) && s.Sla.PrazoResolucao >= desde && s.Sla.PrazoResolucao < antes)
            .Select(s => s.ResponsavelId)
            .ToListAsync(cancellationToken);
}
