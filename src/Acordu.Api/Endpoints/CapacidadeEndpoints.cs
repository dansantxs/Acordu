using Acordu.Api.Seguranca;
using Acordu.Application.Capacidade;
using Acordu.Domain.Acesso;

namespace Acordu.Api.Endpoints;

/// <summary>Capacidade da semana por pessoa (contrato §11c).</summary>
internal static class CapacidadeEndpoints
{
    public static void MapCapacidade(this RouteGroupBuilder interno) =>
        interno.MapGet("/capacidade", async (DateOnly? semana, LeituraCapacidade leitura, CancellationToken cancellationToken) =>
            Results.Ok(await leitura.ObterAsync(semana, cancellationToken)))
            .WithTags("Agenda")
            .RequirePermissao(Permissoes.TarefaVer)
            .Produces<CapacidadeSemanaDto>();
}
