using System.Text;
using Acordu.Api.Seguranca;
using Acordu.Application.Abstractions;
using Acordu.Application.Agenda;
using Acordu.Infrastructure.MultiTenancy;

namespace Acordu.Api.Endpoints;

/// <summary>Agenda da equipe (contrato §11b). Abre com qualquer permissão de leitura de item; cada tipo exige a sua.</summary>
internal static class AgendaEndpoints
{
    public static void MapAgenda(this RouteGroupBuilder interno)
    {
        interno.MapGet("/agenda", async (
            DateOnly? de, DateOnly? ate, Guid? clienteId, Guid? responsavelId, LeituraAgenda leitura, CancellationToken cancellationToken) =>
            Results.Ok(await leitura.ObterAsync(de, ate, clienteId, responsavelId, cancellationToken)))
            .WithTags("Agenda")
            .RequireAlgumaPermissao(LeituraAgenda.PermissoesDeLeitura)
            .Produces<AgendaDto>();

        // Link da agenda assinável da própria pessoa (contrato §11d): o link com o token só aparece no POST.
        var assinatura = interno.MapGroup("/agenda/assinatura").WithTags("Agenda")
            .RequireAlgumaPermissao(LeituraAgenda.PermissoesDeLeitura);
        assinatura.MapGet(string.Empty, async (ComandosAssinaturaAgenda comandos, CancellationToken cancellationToken) =>
            Results.Ok(await comandos.ObterAsync(cancellationToken)))
            .Produces<AssinaturaAgendaDto>();
        assinatura.MapPost(string.Empty, async (
            AssinaturaAgendaRequest corpo, ComandosAssinaturaAgenda comandos, CancellationToken cancellationToken) =>
            Results.Created("/api/v1/agenda/assinatura", await comandos.EmitirAsync(corpo, cancellationToken)))
            .Produces<LinkAgendaDto>(StatusCodes.Status201Created);
        assinatura.MapPatch(string.Empty, async (
            AssinaturaAgendaRequest corpo, ComandosAssinaturaAgenda comandos, CancellationToken cancellationToken) =>
            Results.Ok(await comandos.AlterarModoAsync(corpo, cancellationToken)))
            .Produces<AssinaturaAgendaDto>();
        assinatura.MapDelete(string.Empty, async (ComandosAssinaturaAgenda comandos, CancellationToken cancellationToken) =>
        {
            await comandos.RevogarAsync(cancellationToken);
            return Results.NoContent();
        });
    }

    /// <summary>
    /// Feed ICS sem sessão (Google, Outlook): o token secreto diz o tenant e a pessoa. Token desconhecido, revogado ou de
    /// quem saiu da equipe → 404, sem distinguir.
    /// </summary>
    public static void MapAgendaIcs(this RouteGroupBuilder api) =>
        api.MapGet("/agenda/{token}.ics", async (
            string token, IAssinaturasAgenda assinaturas, ContextoTenant contexto, FeedAgenda feed, HttpResponse resposta,
            CancellationToken cancellationToken) =>
        {
            if (await assinaturas.ResolverAsync(token, cancellationToken) is not { } assinatura)
                return Results.NotFound();
            contexto.DefinirEquipe(assinatura.TenantId);
            if (await feed.GerarAsync(assinatura.UsuarioId, assinatura.Discreta, cancellationToken) is not { } ics)
                return Results.NotFound();
            resposta.Headers.CacheControl = "private, max-age=900";
            return Results.Text(ics, "text/calendar; charset=utf-8", Encoding.UTF8);
        })
            .AllowAnonymous()
            .RequireRateLimiting(ComposicaoSeguranca.LimiteAgenda)
            .WithTags("Agenda")
            .Produces<string>(StatusCodes.Status200OK, "text/calendar")
            .Produces(StatusCodes.Status404NotFound);
}
