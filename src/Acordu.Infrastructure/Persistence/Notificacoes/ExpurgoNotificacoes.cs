using Acordu.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Infrastructure.Persistence.Notificacoes;

/// <summary>Apaga notificações com mais de <see cref="RetencaoDias"/> dias, de todos os tenants (job diário). Ponto revisado de IgnoreQueryFilters.</summary>
public sealed class ExpurgoNotificacoes
{
    public const int RetencaoDias = 90;

    private readonly IServiceScopeFactory _escopos;
    private readonly TimeProvider _relogio;

    public ExpurgoNotificacoes(IServiceScopeFactory escopos, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(escopos);
        ArgumentNullException.ThrowIfNull(relogio);
        _escopos = escopos;
        _relogio = relogio;
    }

    public async Task<int> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        var limite = _relogio.GetUtcNow().AddDays(-RetencaoDias);
        await using var escopo = _escopos.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<AcorduDbContext>().Set<Notificacao>()
            .IgnoreQueryFilters([FiltrosGlobais.Tenant, FiltrosGlobais.Cliente])
            .Where(n => n.CriadaEm < limite)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
