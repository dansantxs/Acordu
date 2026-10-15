using Acordu.Api.Seguranca;
using Acordu.Application.Colaboracao;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;
using Acordu.Domain.Enums;

namespace Acordu.Api.Endpoints;

/// <summary>Quem segue e seguir/deixar de seguir solicitações e tarefas (contrato §7a, RF-COL-03).</summary>
internal static class SeguidoresEndpoints
{
    public static void MapSeguidores(this RouteGroupBuilder interno)
    {
        Mapear(interno, "/solicitacoes", AlvoColaboracao.Solicitacao, Permissoes.SolicitacaoVer, "Solicitações");
        Mapear(interno, "/tarefas", AlvoColaboracao.Tarefa, Permissoes.TarefaVer, "Tarefas");
    }

    private static void Mapear(RouteGroupBuilder interno, string prefixo, AlvoColaboracao alvo, string permissao, string tag)
    {
        var grupo = interno.MapGroup(prefixo + "/{id:guid}/seguidores").WithTags(tag);

        grupo.MapGet(string.Empty, async (Guid id, ComandosSeguimento comandos, CancellationToken cancellationToken) =>
            Results.Ok(await comandos.ListarAsync(alvo, id, cancellationToken)))
            .RequirePermissao(permissao)
            .Produces<IReadOnlyList<Referencia>>();

        grupo.MapPut("/eu", async (Guid id, ComandosSeguimento comandos, CancellationToken cancellationToken) =>
        {
            await comandos.SeguirAsync(alvo, id, cancellationToken);
            return Results.NoContent();
        })
            .RequirePermissao(permissao);

        grupo.MapDelete("/eu", async (Guid id, ComandosSeguimento comandos, CancellationToken cancellationToken) =>
        {
            await comandos.DeixarAsync(alvo, id, cancellationToken);
            return Results.NoContent();
        })
            .RequirePermissao(permissao);
    }
}
