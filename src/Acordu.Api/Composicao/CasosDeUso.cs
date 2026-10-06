using Acordu.Application.Abstractions.Eventos;
using Acordu.Application.Clientes;
using Acordu.Application.Common;
using Acordu.Application.Contratos;
using Acordu.Application.Solicitacoes;
using Acordu.Domain.Events;

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
        services.AddScoped<ComandosSolicitacao>();

        // Reações de estado na mesma transação (modelo §7).
        services.AddScoped<IManipuladorEvento<InteracaoRegistrada>, PrimeiraRespostaERetomada>();
        return services;
    }
}
