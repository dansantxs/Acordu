using Acordu.Application.Abstractions;
using Acordu.Infrastructure.MultiTenancy;
using Acordu.Infrastructure.Persistence;
using Acordu.Infrastructure.Persistence.Eventos;
using Acordu.Infrastructure.Persistence.Outbox;
using Acordu.Infrastructure.Persistence.Protocolo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Acordu.Infrastructure;

/// <summary>
/// Registro da Infrastructure no container. O <see cref="ContextoTenant"/> é por escopo e começa vazio: a Api o define a
/// partir do token; jobs o definem para o tenant que processam.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ContextoTenant>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<ContextoTenant>());
        services.AddDbContext<AcorduDbContext>(options => AcorduDbContextOptions.Configurar(options, connectionString));

        services.AddScoped<DespachanteEventos>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepositorio<>), typeof(Repositorio<>));
        services.AddScoped<IGeradorProtocolo, GeradorProtocolo>();
        services.AddSingleton<ProcessadorOutbox>();
        return services;
    }
}
