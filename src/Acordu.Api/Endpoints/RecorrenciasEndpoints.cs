using Acordu.Api.Seguranca;
using Acordu.Application.Common;
using Acordu.Application.Recorrencias;
using Acordu.Domain.Acesso;
using Acordu.Domain.Enums;

namespace Acordu.Api.Endpoints;

/// <summary>Tarefas recorrentes (contrato §10). <c>DELETE</c> encerra (a recorrência fica como histórico).</summary>
internal static class RecorrenciasEndpoints
{
    public static void MapRecorrencias(this RouteGroupBuilder interno)
    {
        var recorrencias = interno.MapGroup("/recorrencias").WithTags("Recorrências");

        recorrencias.MapGet(string.Empty, async (
            Guid? clienteId, string? status, int? pagina, int? tamanhoPagina, IConsultasRecorrencia consultas,
            CancellationToken cancellationToken) =>
            Results.Ok(await consultas.ListarAsync(
                clienteId, EnumsDaApi.Ler<StatusRecorrencia>(status, nameof(status)), new Paginacao(pagina, tamanhoPagina),
                cancellationToken)))
            .RequirePermissao(Permissoes.RecorrenciaVer)
            .Produces<ResultadoPaginado<RecorrenciaResumoDto>>();

        recorrencias.MapGet("/{id:guid}", async (Guid id, IConsultasRecorrencia consultas, CancellationToken cancellationToken) =>
            Results.Ok(await DetalheAsync(consultas, id, cancellationToken)))
            .RequirePermissao(Permissoes.RecorrenciaVer)
            .Produces<RecorrenciaDto>();

        // Prévia de regra ainda não salva (F4c): POST porque a regra vai no corpo; não grava nada.
        recorrencias.MapPost("/previa", async (PreviaRegraRequest corpo, ComandosRecorrencia comandos, CancellationToken cancellationToken) =>
            Results.Ok(await comandos.PreviaDaRegraAsync(corpo, cancellationToken)))
            .RequirePermissao(Permissoes.RecorrenciaVer)
            .Produces<IReadOnlyList<DateOnly>>();

        recorrencias.MapGet("/{id:guid}/previa", async (
            Guid id, int? quantidade, ComandosRecorrencia comandos, CancellationToken cancellationToken) =>
            Results.Ok(await comandos.PreviaAsync(id, quantidade, cancellationToken)))
            .RequirePermissao(Permissoes.RecorrenciaVer)
            .Produces<IReadOnlyList<DateOnly>>();

        recorrencias.MapPost(string.Empty, async (
            NovaRecorrenciaRequest corpo, ComandosRecorrencia comandos, IConsultasRecorrencia consultas,
            CancellationToken cancellationToken) =>
        {
            var id = await comandos.CriarAsync(corpo, cancellationToken);
            return Results.Created($"/api/v1/recorrencias/{id}", await DetalheAsync(consultas, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.RecorrenciaGerenciar)
            .Produces<RecorrenciaDto>(StatusCodes.Status201Created);

        recorrencias.MapPatch("/{id:guid}", async (
            Guid id, EdicaoRecorrenciaRequest corpo, HttpRequest requisicao, ComandosRecorrencia comandos,
            IConsultasRecorrencia consultas, CancellationToken cancellationToken) =>
        {
            await comandos.EditarAsync(id, corpo, Versoes.IfMatch(requisicao), cancellationToken);
            return Results.Ok(await DetalheAsync(consultas, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.RecorrenciaGerenciar)
            .Produces<RecorrenciaDto>();

        recorrencias.MapDelete("/{id:guid}", async (
            Guid id, HttpRequest requisicao, ComandosRecorrencia comandos, CancellationToken cancellationToken) =>
        {
            await comandos.EncerrarAsync(id, Versoes.IfMatch(requisicao), cancellationToken);
            return Results.NoContent();
        })
            .RequirePermissao(Permissoes.RecorrenciaGerenciar);

        MapTransicao(recorrencias, "/pausa", (c, id, versao, ct) => c.PausarAsync(id, versao, ct));
        MapTransicao(recorrencias, "/retomada", (c, id, versao, ct) => c.RetomarAsync(id, versao, ct));
    }

    /// <summary>Ação de status (convenções §4): <c>If-Match</c> opcional, devolve a recorrência atualizada.</summary>
    private static void MapTransicao(
        RouteGroupBuilder recorrencias,
        string acao,
        Func<ComandosRecorrencia, Guid, string?, CancellationToken, Task> executar) =>
        recorrencias.MapPost("/{id:guid}" + acao, async (
            Guid id, HttpRequest requisicao, ComandosRecorrencia comandos, IConsultasRecorrencia consultas,
            CancellationToken cancellationToken) =>
        {
            await executar(comandos, id, Versoes.IfMatch(requisicao), cancellationToken);
            return Results.Ok(await DetalheAsync(consultas, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.RecorrenciaGerenciar)
            .Produces<RecorrenciaDto>();

    private static async Task<RecorrenciaDto> DetalheAsync(IConsultasRecorrencia consultas, Guid id, CancellationToken cancellationToken) =>
        await consultas.DetalharAsync(id, cancellationToken) ?? throw new NaoEncontradoException("Recorrência não encontrada.");
}
