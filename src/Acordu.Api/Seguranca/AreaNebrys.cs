using Acordu.Application.Nebrys;
using Microsoft.AspNetCore.Authorization;

namespace Acordu.Api.Seguranca;

/// <summary>
/// Barreira da área da Nebrys (ADR 0014, item 4; F14-63). Dois sentidos, antes do contexto de tenant, do rate limit e da
/// autorização:
/// <list type="bullet">
/// <item>em <c>/api/v1/nebrys</c>, qualquer requisição sem sessão de operação (equipe, contato, link ou ninguém) recebe o
/// mesmo <c>404</c> de uma rota inexistente, qualquer que seja o método ou o caminho: nem o <c>405</c> nem o <c>401</c>
/// revelam a área. Só a abertura da sessão (<see cref="AberturaDaSessaoOperacao"/>) passa, e ela também responde
/// <c>404</c> à credencial recusada;</item>
/// <item>fora da área, a sessão de operação recebe <c>404</c> em toda rota da API que não é pública: ela não tem tenant e
/// não serve para mais nada.</item>
/// </list>
/// O caminho é comparado sem diferenciar maiúsculas, como o roteamento.
/// </summary>
internal static class AreaNebrys
{
    public const string Prefixo = "/api/v1/nebrys";

    public static Task ConferirAsync(HttpContext http, Func<Task> proximo)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(proximo);
        var operacao = ClaimsAcordu.EhOperacao(http.User);
        var endpoint = http.GetEndpoint();
        if (NaArea(http.Request.Path))
        {
            return operacao || endpoint?.Metadata.GetMetadata<AberturaDaSessaoOperacao>() is not null
                ? proximo()
                : NaoEncontrado(http);
        }

        if (operacao
            && http.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            && endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is null)
        {
            return NaoEncontrado(http);
        }

        return proximo();
    }

    public static bool NaArea(PathString caminho) => caminho.StartsWithSegments(Prefixo, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Filtro das rotas da área (menos a abertura da sessão): a conta do token continua operadora (na lista, com o e-mail
    /// confirmado). Tirar alguém da lista vale na revisão seguinte, sem esperar o token de 30 min expirar.
    /// </summary>
    public static async ValueTask<object?> ConferirOperadorAsync(EndpointFilterInvocationContext contexto, EndpointFilterDelegate proximo)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(proximo);
        var http = contexto.HttpContext;
        if (!ClaimsAcordu.EhOperacao(http.User)
            || ClaimsAcordu.IdDe(http.User, ClaimsAcordu.Usuario) is not { } operador
            || !await http.RequestServices.GetRequiredService<IServicoOperacao>().EhOperadorAsync(operador, http.RequestAborted))
        {
            return Results.NotFound();
        }

        return await proximo(contexto);
    }

    /// <summary>Sem corpo: o <c>UseStatusCodePages</c> escreve o mesmo Problem Details de uma rota inexistente.</summary>
    private static Task NaoEncontrado(HttpContext http)
    {
        http.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }
}

/// <summary>Marca da rota <c>POST /nebrys/sessao</c>, a única da área aberta a quem ainda não tem a sessão de operação.</summary>
internal sealed class AberturaDaSessaoOperacao;
