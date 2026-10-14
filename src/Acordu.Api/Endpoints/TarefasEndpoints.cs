using Acordu.Api.Seguranca;
using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Application.Tarefas;
using Acordu.Domain.Acesso;
using Acordu.Domain.Enums;

namespace Acordu.Api.Endpoints;

/// <summary>Tarefas e checklist (contrato §8.1). O quadro é o mesmo GET agrupado por status no front.</summary>
internal static class TarefasEndpoints
{
    public static void MapTarefas(this RouteGroupBuilder interno)
    {
        var tarefas = interno.MapGroup("/tarefas").WithTags("Tarefas");

        tarefas.MapGet(string.Empty, async (
            string? status, Guid? responsavelId, Guid? solicitacaoId, Guid? projetoId, Guid? clienteId, DateOnly? prazoAte, bool? minhas,
            int? pagina, int? tamanhoPagina, ITenantContext contexto, IConsultasTarefa consultas, CancellationToken cancellationToken) =>
        {
            var filtro = new FiltroTarefas(
                Status: status?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => EnumsDaApi.Ler<StatusTarefa>(s, nameof(status))!.Value)
                    .ToList(),
                ResponsavelId: minhas == true ? contexto.UsuarioId : responsavelId,
                SolicitacaoId: solicitacaoId,
                ProjetoId: projetoId,
                ClienteId: clienteId,
                PrazoAte: prazoAte);
            return Results.Ok(await consultas.ListarAsync(filtro, new Paginacao(pagina, tamanhoPagina), cancellationToken));
        })
            .RequirePermissao(Permissoes.TarefaVer)
            .Produces<ResultadoPaginado<TarefaResumoDto>>();

        tarefas.MapGet("/{id:guid}", async (Guid id, IConsultasTarefa consultas, CancellationToken cancellationToken) =>
            Results.Ok(await DetalheAsync(consultas, id, cancellationToken)))
            .RequirePermissao(Permissoes.TarefaVer)
            .Produces<TarefaDto>();

        tarefas.MapPost(string.Empty, async (
            NovaTarefaRequest corpo, ComandosTarefa comandos, IConsultasTarefa consultas, CancellationToken cancellationToken) =>
        {
            var id = await comandos.CriarAsync(corpo, cancellationToken);
            return Results.Created($"/api/v1/tarefas/{id}", await DetalheAsync(consultas, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.TarefaCriar)
            .Produces<TarefaDto>(StatusCodes.Status201Created);

        tarefas.MapPatch("/{id:guid}", async (
            Guid id, EdicaoTarefaRequest corpo, HttpRequest requisicao, ComandosTarefa comandos, IConsultasTarefa consultas,
            CancellationToken cancellationToken) =>
        {
            await comandos.EditarAsync(id, corpo, Versoes.IfMatch(requisicao), cancellationToken);
            return Results.Ok(await DetalheAsync(consultas, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.TarefaEditar)
            .Produces<TarefaDto>();

        MapTransicao(tarefas, "/inicio", (c, id, versao, ct) => c.IniciarAsync(id, versao, ct));
        MapTransicao(tarefas, "/conclusao", (c, id, versao, ct) => c.ConcluirAsync(id, versao, ct));
        MapTransicao(tarefas, "/reabertura", (c, id, versao, ct) => c.ReabrirAsync(id, versao, ct));
        MapTransicao(tarefas, "/cancelamento", (c, id, versao, ct) => c.CancelarAsync(id, versao, ct));

        tarefas.MapPost("/{id:guid}/itens", async (
            Guid id, ItemChecklistRequest corpo, ComandosTarefa comandos, IConsultasTarefa consultas, CancellationToken cancellationToken) =>
        {
            var itemId = await comandos.AdicionarItemAsync(id, corpo, cancellationToken);
            var tarefa = await DetalheAsync(consultas, id, cancellationToken);
            return Results.Created($"/api/v1/tarefas/{id}", tarefa.Checklist.Single(i => i.Id == itemId));
        })
            .RequirePermissao(Permissoes.TarefaEditar)
            .Produces<ItemChecklistDto>(StatusCodes.Status201Created);

        tarefas.MapPatch("/{id:guid}/itens/{itemId:guid}", async (
            Guid id, Guid itemId, ItemChecklistRequest corpo, ComandosTarefa comandos, IConsultasTarefa consultas,
            CancellationToken cancellationToken) =>
        {
            await comandos.EditarItemAsync(id, itemId, corpo, cancellationToken);
            var tarefa = await DetalheAsync(consultas, id, cancellationToken);
            return Results.Ok(tarefa.Checklist.Single(i => i.Id == itemId));
        })
            .RequirePermissao(Permissoes.TarefaEditar)
            .Produces<ItemChecklistDto>();

        tarefas.MapDelete("/{id:guid}/itens/{itemId:guid}", async (
            Guid id, Guid itemId, ComandosTarefa comandos, CancellationToken cancellationToken) =>
        {
            await comandos.RemoverItemAsync(id, itemId, cancellationToken);
            return Results.NoContent();
        })
            .RequirePermissao(Permissoes.TarefaEditar);

        // Comentários internos (contrato §8.1a): nunca vão ao portal.
        tarefas.MapGet("/{id:guid}/comentarios", async (Guid id, IConsultasComentariosTarefa consultas, CancellationToken cancellationToken) =>
            Results.Ok(await consultas.DaTarefaAsync(id, cancellationToken) ?? throw new NaoEncontradoException("Tarefa não encontrada.")))
            .RequirePermissao(Permissoes.TarefaVer)
            .Produces<IReadOnlyList<ComentarioTarefaDto>>();

        // JSON { texto } ou multipart (texto, anexos[]).
        tarefas.MapPost("/{id:guid}/comentarios", async (
            Guid id, HttpRequest requisicao, ComandosComentarioTarefa comandos, IConsultasComentariosTarefa consultas,
            CancellationToken cancellationToken) =>
        {
            var formulario = await FormularioComAnexos.LerAsync(requisicao, cancellationToken);
            var corpo = formulario is null
                ? await CorpoOpcional.LerAsync<ComentarioTarefaRequest>(requisicao, cancellationToken)
                    ?? throw new BadHttpRequestException("Envie o comentário em JSON ou multipart/form-data.")
                : new ComentarioTarefaRequest(formulario.Campo("texto"));
            var comentarioId = await comandos.ComentarAsync(id, corpo, formulario?.Arquivos, cancellationToken);
            var lista = await consultas.DaTarefaAsync(id, cancellationToken);
            return Results.Created($"/api/v1/tarefas/{id}/comentarios", lista!.Single(c => c.Id == comentarioId));
        })
            .RequirePermissao(Permissoes.TarefaEditar)
            .Accepts<ComentarioTarefaRequest>("application/json", "multipart/form-data")
            .Produces<ComentarioTarefaDto>(StatusCodes.Status201Created);

        tarefas.MapDelete("/{id:guid}/comentarios/{comentarioId:guid}", async (
            Guid id, Guid comentarioId, ComandosComentarioTarefa comandos, CancellationToken cancellationToken) =>
        {
            await comandos.ExcluirAsync(id, comentarioId, cancellationToken);
            return Results.NoContent();
        })
            .RequirePermissao(Permissoes.TarefaEditar);
    }

    private static void MapTransicao(
        RouteGroupBuilder tarefas, string acao, Func<ComandosTarefa, Guid, string?, CancellationToken, Task> executar) =>
        tarefas.MapPost("/{id:guid}" + acao, async (
            Guid id, HttpRequest requisicao, ComandosTarefa comandos, IConsultasTarefa consultas, CancellationToken cancellationToken) =>
        {
            await executar(comandos, id, Versoes.IfMatch(requisicao), cancellationToken);
            return Results.Ok(await DetalheAsync(consultas, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.TarefaEditar)
            .Produces<TarefaDto>();

    private static async Task<TarefaDto> DetalheAsync(IConsultasTarefa consultas, Guid id, CancellationToken cancellationToken) =>
        await consultas.DetalharAsync(id, cancellationToken) ?? throw new NaoEncontradoException("Tarefa não encontrada.");
}
