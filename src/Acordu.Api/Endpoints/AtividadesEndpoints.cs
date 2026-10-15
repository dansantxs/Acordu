using Acordu.Api.Seguranca;
using Acordu.Application.Atividades;
using Acordu.Domain.Acesso;

namespace Acordu.Api.Endpoints;

/// <summary>Feed de atividade da equipe (contrato §11f).</summary>
internal static class AtividadesEndpoints
{
    public static void MapAtividades(this RouteGroupBuilder interno) =>
        interno.MapGet("/atividades", async (
            Guid? clienteId, Guid? pessoaId, long? antes, LeituraAtividades leitura, CancellationToken cancellationToken) =>
            Results.Ok(await leitura.ListarAsync(clienteId, pessoaId, antes, cancellationToken)))
            .WithTags("Atividades")
            .RequireAlgumaPermissao(Permissoes.SolicitacaoVer, Permissoes.TarefaVer)
            .Produces<PaginaAtividadesDto>();
}
