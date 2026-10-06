using Acordu.Application.Clientes;
using Acordu.Application.Common;
using Acordu.Application.Contratos;

namespace Acordu.Api.Composicao;

/// <summary>
/// Casos de uso da Application. Registrados aqui (composition root) para a Application seguir sem pacotes, só com o
/// Domain, e compilar isolada.
/// </summary>
internal static class CasosDeUso
{
    public static IServiceCollection AddCasosDeUso(this IServiceCollection services)
    {
        services.AddScoped<RelogioDoTenant>();
        services.AddScoped<ComandosCliente>();
        services.AddScoped<ComandosContrato>();
        return services;
    }
}
