using Acordu.Api.Erros;
using Acordu.Application.Abstractions;
using Acordu.Application.Portal;

namespace Acordu.Api.Seguranca;

/// <summary>
/// Filtro do grupo <c>/api/v1/portal</c>: o contato precisa continuar com acesso no banco (vínculo e contato ativos).
/// Revogar o acesso vale na requisição seguinte, sem esperar o access token expirar → 401. O resultado fica no
/// <see cref="IPortalAtual"/> da requisição e é reaproveitado pelos casos de uso. A sessão de link só passa nas rotas
/// marcadas com <see cref="Politicas.PermitirLink{TBuilder}"/> (403 nas demais), e o acesso dela precisa continuar
/// válido (revogado ou expirado → 401).
/// </summary>
internal static class SessaoPortal
{
    public static async ValueTask<object?> ConferirAsync(EndpointFilterInvocationContext contexto, EndpointFilterDelegate proximo)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(proximo);
        var http = contexto.HttpContext;
        if (http.RequestServices.GetRequiredService<ITenantContext>().AcessoPortalId is not null
            && http.GetEndpoint()?.Metadata.GetMetadata<PermiteLink>() is null)
        {
            return Results.Problem(
                "O link de acesso abre só a solicitação para a qual foi enviado.",
                statusCode: StatusCodes.Status403Forbidden,
                title: "Sem permissão",
                type: ManipuladorExcecoes.BaseTipos + "sem-permissao");
        }

        var portal = http.RequestServices.GetRequiredService<IPortalAtual>();
        if (await portal.ObterAsync(http.RequestAborted) is null)
        {
            return Results.Problem(
                "Seu acesso ao portal foi removido. Fale com quem atende você.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Não autenticado",
                type: ManipuladorExcecoes.BaseTipos + "nao-autenticado");
        }

        return await proximo(contexto);
    }
}
