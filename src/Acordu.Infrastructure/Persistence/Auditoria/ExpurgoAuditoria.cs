using Acordu.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Infrastructure.Persistence.Auditoria;

/// <summary>
/// Remove registros com mais de <see cref="RetencaoMeses"/> meses, de todos os tenants e os sem tenant (job diário):
/// a trilha guarda IP e e-mail digitado, então fica só o tempo necessário (LGPD). Ponto revisado de IgnoreQueryFilters.
/// </summary>
public sealed class ExpurgoAuditoria
{
    public const int RetencaoMeses = 12;

    private readonly IServiceScopeFactory _escopos;
    private readonly TimeProvider _relogio;

    public ExpurgoAuditoria(IServiceScopeFactory escopos, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(escopos);
        ArgumentNullException.ThrowIfNull(relogio);
        _escopos = escopos;
        _relogio = relogio;
    }

    /// <summary>Devolve quantos registros removeu.</summary>
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        var limite = _relogio.GetUtcNow().AddMonths(-RetencaoMeses);
        await using var escopo = _escopos.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<AcorduDbContext>().Set<RegistroAuditoria>()
            .IgnoreQueryFilters([FiltrosGlobais.Tenant, FiltrosGlobais.Cliente])
            .Where(r => r.OcorridoEm < limite)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
