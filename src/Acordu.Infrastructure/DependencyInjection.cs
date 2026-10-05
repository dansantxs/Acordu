using Acordu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Infrastructure;

/// <summary>Registro da Infrastructure no container. O <c>ITenantContext</c> é registrado pela Api.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddDbContext<AcorduDbContext>(options => AcorduDbContextOptions.Configurar(options, connectionString));
        return services;
    }
}
