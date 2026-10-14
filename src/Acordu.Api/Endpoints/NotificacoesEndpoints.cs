using Acordu.Api.Seguranca;
using Acordu.Application.Common;
using Acordu.Application.Notificacoes;

namespace Acordu.Api.Endpoints;

/// <summary>Sino e preferências de e-mail da própria pessoa (contrato §11; RF-NOT-03/04). Sem permissão específica.</summary>
internal static class NotificacoesEndpoints
{
    public static void MapNotificacoes(this RouteGroupBuilder interno)
    {
        var notificacoes = interno.MapGroup("/notificacoes").WithTags("Notificações").RequireAuthorization(Politicas.Usuario);

        notificacoes.MapGet(string.Empty, async (
            bool? naoLidas, int? pagina, int? tamanhoPagina, ComandosNotificacoes comandos, CancellationToken cancellationToken) =>
            Results.Ok(await comandos.ListarAsync(naoLidas == true, new Paginacao(pagina, tamanhoPagina), cancellationToken)))
            .Produces<NotificacoesDto>();

        notificacoes.MapPost("/{id:guid}/leitura", async (Guid id, ComandosNotificacoes comandos, CancellationToken cancellationToken) =>
        {
            await comandos.MarcarLidaAsync(id, cancellationToken);
            return Results.NoContent();
        });

        notificacoes.MapPost("/leitura", async (ComandosNotificacoes comandos, CancellationToken cancellationToken) =>
        {
            await comandos.MarcarTodasAsync(cancellationToken);
            return Results.NoContent();
        });

        notificacoes.MapGet("/preferencias", async (ComandosNotificacoes comandos, CancellationToken cancellationToken) =>
            Results.Ok(await comandos.PreferenciasAsync(cancellationToken)))
            .Produces<PreferenciasDto>();

        notificacoes.MapPut("/preferencias", async (PreferenciasDto corpo, ComandosNotificacoes comandos, CancellationToken cancellationToken) =>
            Results.Ok(await comandos.SalvarPreferenciasAsync(corpo, cancellationToken)))
            .Produces<PreferenciasDto>();
    }
}
