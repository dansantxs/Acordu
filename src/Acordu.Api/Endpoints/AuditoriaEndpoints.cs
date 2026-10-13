using Acordu.Api.Seguranca;
using Acordu.Application.Auditoria;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;

namespace Acordu.Api.Endpoints;

/// <summary>Trilha de auditoria do tenant (contrato §11a). Só leitura; registros com mais de 12 meses são expurgados.</summary>
internal static class AuditoriaEndpoints
{
    public static void MapAuditoria(this RouteGroupBuilder interno) =>
        interno.MapGet("/auditoria", async (
            DateOnly? de, DateOnly? ate, string? acao, Guid? usuarioId, Guid? alvoId, int? pagina, int? tamanhoPagina,
            IConsultasAuditoria consultas, CancellationToken cancellationToken) =>
            Results.Ok(await consultas.ListarAsync(
                new FiltroAuditoria(de, ate, acao, usuarioId, alvoId), new Paginacao(pagina, tamanhoPagina), cancellationToken)))
            .WithTags("Auditoria")
            .RequirePermissao(Permissoes.AuditoriaVer)
            .Produces<ResultadoPaginado<RegistroAuditoriaDto>>();
}
