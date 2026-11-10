using Acordu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Infrastructure.Operacao;

/// <summary>Apaga as ocorrências de erro sem repetição há 90 dias (ADR 0012). Passo do job diário.</summary>
public sealed class ExpurgoOcorrenciasErro
{
    public const int RetencaoDias = 90;

    private readonly IServiceScopeFactory _escopos;
    private readonly TimeProvider _relogio;

    public ExpurgoOcorrenciasErro(IServiceScopeFactory escopos, TimeProvider relogio)
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
        return await escopo.ServiceProvider.GetRequiredService<AcorduDbContext>().Set<OcorrenciaErro>()
            .Where(o => o.UltimaEm < limite)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
