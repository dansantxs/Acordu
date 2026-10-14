using Acordu.Api.Seguranca;
using Acordu.Application.Agenda;

namespace Acordu.Api.Endpoints;

/// <summary>Agenda da equipe (contrato §11b). Abre com qualquer permissão de leitura de item; cada tipo exige a sua.</summary>
internal static class AgendaEndpoints
{
    public static void MapAgenda(this RouteGroupBuilder interno) =>
        interno.MapGet("/agenda", async (
            DateOnly? de, DateOnly? ate, Guid? clienteId, Guid? responsavelId, LeituraAgenda leitura, CancellationToken cancellationToken) =>
            Results.Ok(await leitura.ObterAsync(de, ate, clienteId, responsavelId, cancellationToken)))
            .WithTags("Agenda")
            .RequireAlgumaPermissao(LeituraAgenda.PermissoesDeLeitura)
            .Produces<AgendaDto>();
}
