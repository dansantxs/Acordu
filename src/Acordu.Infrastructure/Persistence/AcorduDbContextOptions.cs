using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence;

/// <summary>Configuração única do provedor (Api, design-time e testes usam a mesma).</summary>
public static class AcorduDbContextOptions
{
    public static DbContextOptionsBuilder Configurar(DbContextOptionsBuilder builder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return builder
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();
    }
}
