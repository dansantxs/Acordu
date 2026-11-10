using Acordu.Api.Operacao;
using Acordu.Api.Seguranca;
using Acordu.Application.Sugestoes;

namespace Acordu.Api.Endpoints;

/// <summary>Corpo de "Sugerir melhoria": o texto e a tela de onde veio (rota do app).</summary>
internal sealed record SugestaoRequest(string Texto, string? Tela);

/// <summary>
/// "Sugerir melhoria" (contrato §16; RF-SUG-01; spec do F13, F13e): só <c>POST</c>, para qualquer pessoa da equipe com
/// conta. Nenhuma rota lista ou devolve sugestões (F13-27).
/// </summary>
internal static class SugestoesEndpoints
{
    public static void MapSugestoes(this RouteGroupBuilder interno) =>
        interno.MapPost("/sugestoes", async (SugestaoRequest corpo, ComandosSugestao comandos, CancellationToken cancellationToken) =>
            {
                ArgumentNullException.ThrowIfNull(corpo);
                // A tela vem do app como rota; fora do padrão, fica sem (não é o que importa na sugestão).
                var tela = ErrosWebEndpoints.RotaModelo(corpo.Tela) is var modelo && modelo.StartsWith('/') ? modelo : null;
                var criada = await comandos.EnviarAsync(corpo.Texto ?? "", tela, cancellationToken);
                return Results.Created((string?)null, criada);
            })
            .WithTags("Sugestões")
            .RequireAuthorization(Politicas.Usuario)
            .Produces<SugestaoCriadaDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
