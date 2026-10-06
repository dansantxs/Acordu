using Acordu.Application.Abstractions.Eventos;
using Acordu.Application.Apontamentos;
using Acordu.Application.Clientes;
using Acordu.Application.Common;
using Acordu.Application.Contratos;
using Acordu.Application.Projetos;
using Acordu.Application.Solicitacoes;
using Acordu.Application.Tarefas;
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
        services.AddScoped<ComandosTarefa>();
        services.AddScoped<ComandosProjeto>();
        services.AddScoped<ComandosApontamento>();

        // Reações de estado na mesma transação (modelo §7).
        services.AddScoped<IManipuladorEvento<InteracaoRegistrada>, PrimeiraRespostaERetomada>();
        return services;
    }
}
