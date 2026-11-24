using Acordu.Api.Seguranca;
using Acordu.Application.Abstractions;
using Acordu.Application.Autenticacao;
using Acordu.Application.ListaEspera;

namespace Acordu.Api.Endpoints;

/// <summary>
/// Lista de espera pública (contrato §16; RF-AUT-08, F14-52 a F14-55; design do F14, D12): quem quer usar o Acordu enquanto
/// o cadastro é só por convite. Sem login, com o Turnstile (ação <c>lista-espera</c>) antes de tudo e o limite próprio por
/// IP (F14-54). Responde <c>202</c> sem corpo, com ou sem entrada nova (F14-53). A Nebrys vê e converte a lista na área
/// (<see cref="NebrysEndpoints"/>).
/// </summary>
internal static class ListaEsperaEndpoints
{
    public static void MapListaEspera(this RouteGroupBuilder api) =>
        api.MapPost("/lista-espera", async (
            ListaEsperaRequest corpo, IVerificacaoHumana humana, IOrigemRequisicao origem, InscricaoListaEspera inscricao,
            CancellationToken cancellationToken) =>
        {
            await DesafioHumano.ExigirAsync(humana, origem, corpo.TokenTurnstile, AcoesVerificacaoHumana.ListaEspera, cancellationToken);
            await inscricao.InscreverAsync(corpo, cancellationToken);
            return Results.Accepted();
        })
            .AllowAnonymous()
            .RequireRateLimiting(ComposicaoSeguranca.LimiteListaEspera)
            .WithTags("Operação")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
