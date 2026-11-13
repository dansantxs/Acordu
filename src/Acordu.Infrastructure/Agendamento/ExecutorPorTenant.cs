using Acordu.Domain.Enums;
using Acordu.Infrastructure.MultiTenancy;
using Acordu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Acordu.Infrastructure.Agendamento;

/// <summary>
/// Roda um job em cada tenant gravável (fora do só leitura, RF-EMP-02), num escopo próprio com o contexto fixado nele (o
/// sistema agindo, sem usuário). Falha num tenant é registrada e não impede os demais; a próxima execução tenta de novo
/// (jobs idempotentes).
/// </summary>
public sealed partial class ExecutorPorTenant
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ILogger<ExecutorPorTenant> _logger;

    public ExecutorPorTenant(IServiceScopeFactory escopos, ILogger<ExecutorPorTenant> logger)
    {
        ArgumentNullException.ThrowIfNull(escopos);
        ArgumentNullException.ThrowIfNull(logger);
        _escopos = escopos;
        _logger = logger;
    }

    public async Task<ResultadoPorTenant> ExecutarAsync(
        string job, Func<IServiceProvider, CancellationToken, Task<int>> passo, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(job);
        ArgumentNullException.ThrowIfNull(passo);

        List<Guid> tenants;
        await using (var escopo = _escopos.CreateAsyncScope())
        {
            // Só os Ids, de todos os tenants graváveis: ponto revisado de IgnoreQueryFilters (sem tenant no contexto). A
            // empresa só leitura fica de fora (ADR 0015, item 4): a guarda recusaria a gravação e o job contaria falha.
            tenants = await escopo.ServiceProvider.GetRequiredService<AcorduDbContext>().Tenants
                .IgnoreQueryFilters([FiltrosGlobais.Tenant])
                .Where(t => !SituacaoEmpresaExtensions.SituacoesSomenteLeitura.Contains(t.Situacao))
                .OrderBy(t => t.Id)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);
        }

        var total = 0;
        var falhas = 0;
        foreach (var tenant in tenants)
        {
            await using var escopo = _escopos.CreateAsyncScope();
            escopo.ServiceProvider.GetRequiredService<ContextoTenant>().DefinirEquipe(tenant);
            try
            {
                total += await passo(escopo.ServiceProvider, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                falhas++;
                FalhaNoTenant(_logger, job, tenant, ex);
            }
        }

        return new ResultadoPorTenant(tenants.Count, total, falhas);
    }

    [LoggerMessage(EventId = 200, Level = LogLevel.Error, Message = "Job {Job} falhou no tenant {TenantId}.")]
    private static partial void FalhaNoTenant(ILogger logger, string job, Guid tenantId, Exception excecao);
}

/// <summary>Tenants percorridos, itens tratados e tenants com falha.</summary>
public sealed record ResultadoPorTenant(int Tenants, int Tratados, int Falhas);
