using Acordu.Api.Seguranca;
using Acordu.Application.Common;
using Acordu.Application.Equipe;
using Acordu.Domain.Acesso;

namespace Acordu.Api.Endpoints;

/// <summary>Usuários e convites da equipe (contrato §3). O aceite do convite é público, em <c>/auth/convite</c>.</summary>
internal static class EquipeEndpoints
{
    public static void MapEquipe(this RouteGroupBuilder interno)
    {
        var usuarios = interno.MapGroup("/usuarios").WithTags("Equipe");

        usuarios.MapGet(string.Empty, async (IConsultasEquipe consultas, CancellationToken cancellationToken) =>
            Results.Ok(await consultas.UsuariosAsync(cancellationToken)))
            .RequireAlgumaPermissao(Permissoes.UsuarioConvidar, Permissoes.UsuarioRemover)
            .Produces<IReadOnlyList<UsuarioEquipeDto>>();

        usuarios.MapPatch("/{id:guid}/papel", async (
            Guid id, TrocaPapelRequest corpo, ComandosEquipe comandos, IConsultasEquipe consultas, CancellationToken cancellationToken) =>
        {
            await comandos.TrocarPapelAsync(id, corpo, cancellationToken);
            return Results.Ok(await consultas.UsuarioAsync(id, cancellationToken)
                ?? throw new NaoEncontradoException("Pessoa não encontrada na equipe."));
        })
            .RequirePermissao(Permissoes.PapelGerenciar)
            .Produces<UsuarioEquipeDto>();

        usuarios.MapDelete("/{id:guid}", async (Guid id, ComandosEquipe comandos, CancellationToken cancellationToken) =>
        {
            await comandos.RemoverAsync(id, cancellationToken);
            return Results.NoContent();
        })
            .RequirePermissao(Permissoes.UsuarioRemover);

        var convites = interno.MapGroup("/convites").WithTags("Equipe");

        convites.MapGet(string.Empty, async (IConsultasEquipe consultas, CancellationToken cancellationToken) =>
            Results.Ok(await consultas.ConvitesPendentesAsync(cancellationToken)))
            .RequirePermissao(Permissoes.UsuarioConvidar)
            .Produces<IReadOnlyList<ConviteEquipeDto>>();

        // 202: o convite sai por e-mail depois da resposta (outbox).
        convites.MapPost(string.Empty, async (
            ConviteEquipeRequest corpo, ComandosEquipe comandos, IConsultasEquipe consultas, CancellationToken cancellationToken) =>
        {
            var id = await comandos.ConvidarAsync(corpo, cancellationToken);
            var convite = (await consultas.ConvitesPendentesAsync(cancellationToken)).Single(c => c.Id == id);
            return Results.Accepted("/api/v1/convites", convite);
        })
            .RequirePermissao(Permissoes.UsuarioConvidar)
            .Produces<ConviteEquipeDto>(StatusCodes.Status202Accepted);

        convites.MapDelete("/{id:guid}", async (Guid id, ComandosEquipe comandos, CancellationToken cancellationToken) =>
        {
            await comandos.RevogarConviteAsync(id, cancellationToken);
            return Results.NoContent();
        })
            .RequirePermissao(Permissoes.UsuarioConvidar);
    }
}
