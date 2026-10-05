using Acordu.Infrastructure.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>Container real da Infrastructure, com relógio controlável e registros extras por teste.</summary>
internal sealed class Servicos : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    public Servicos(BancoFixture banco, Action<IServiceCollection>? registrar = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(Relogio);
        registrar?.Invoke(services);
        services.AddInfrastructure(banco.ConnectionString);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    public RelogioDeTeste Relogio { get; } = new(new DateTimeOffset(2026, 10, 6, 13, 0, 0, TimeSpan.Zero));

    public IServiceProvider Raiz => _provider;

    /// <summary>Escopo com o contexto definido para a equipe do tenant.</summary>
    public AsyncServiceScope DaEquipe(Guid tenantId)
    {
        var escopo = _provider.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<ContextoTenant>().DefinirEquipe(tenantId);
        return escopo;
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}

/// <summary>Relógio parado, avançado à mão.</summary>
internal sealed class RelogioDeTeste(DateTimeOffset inicio) : TimeProvider
{
    public DateTimeOffset Agora { get; set; } = inicio;

    public override DateTimeOffset GetUtcNow() => Agora;
}
