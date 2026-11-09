using Acordu.Api.Seguranca;
using Acordu.Application.Common;
using Acordu.Application.Projetos;
using Acordu.Application.Tarefas;
using Acordu.Domain.Acesso;
using Acordu.Domain.Enums;

namespace Acordu.Api.Endpoints;

/// <summary>Projetos com progresso (contrato §8.2).</summary>
internal static class ProjetosEndpoints
{
    public static void MapProjetos(this RouteGroupBuilder interno)
    {
        var projetos = interno.MapGroup("/projetos").WithTags("Projetos");

        projetos.MapGet(string.Empty, async (
            Guid? clienteId, string? status, int? pagina, int? tamanhoPagina, IConsultasProjeto consultas, CancellationToken cancellationToken) =>
            Results.Ok(await consultas.ListarAsync(
                clienteId, EnumsDaApi.Ler<StatusProjeto>(status, nameof(status)), new Paginacao(pagina, tamanhoPagina), cancellationToken)))
            .RequirePermissao(Permissoes.ProjetoVer)
            .Produces<ResultadoPaginado<ProjetoResumoDto>>();

        projetos.MapGet("/{id:guid}", async (Guid id, IConsultasProjeto consultas, CancellationToken cancellationToken) =>
            Results.Ok(await DetalheAsync(consultas, id, cancellationToken)))
            .RequirePermissao(Permissoes.ProjetoVer)
            .Produces<ProjetoDto>();

        projetos.MapPost(string.Empty, async (
            NovoProjetoRequest corpo, ComandosProjeto comandos, IConsultasProjeto consultas, CancellationToken cancellationToken) =>
        {
            var id = await comandos.CriarAsync(corpo, cancellationToken);
            return Results.Created($"/api/v1/projetos/{id}", await DetalheAsync(consultas, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.ProjetoGerenciar)
            .Produces<ProjetoDto>(StatusCodes.Status201Created);

        projetos.MapPatch("/{id:guid}", async (
            Guid id, EdicaoProjetoRequest corpo, HttpRequest requisicao, ComandosProjeto comandos, IConsultasProjeto consultas,
            CancellationToken cancellationToken) =>
        {
            await comandos.EditarAsync(id, corpo, Versoes.IfMatch(requisicao), cancellationToken);
            return Results.Ok(await DetalheAsync(consultas, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.ProjetoGerenciar)
            .Produces<ProjetoDto>();

        MapTransicao(projetos, "/conclusao", (c, id, versao, tarefas, ct) => c.ConcluirAsync(id, versao, tarefas, ct));
        MapTransicao(projetos, "/cancelamento", (c, id, versao, tarefas, ct) => c.CancelarAsync(id, versao, tarefas, ct));
    }

    // Corpo opcional (F11-27): sem corpo, como antes, as tarefas ficam como estão.
    private static void MapTransicao(
        RouteGroupBuilder projetos,
        string acao,
        Func<ComandosProjeto, Guid, string?, TarefasAbertasRequest?, CancellationToken, Task> executar) =>
        projetos.MapPost("/{id:guid}" + acao, async (
            Guid id, EncerramentoProjetoRequest? corpo,
            HttpRequest requisicao, ComandosProjeto comandos, IConsultasProjeto consultas, CancellationToken cancellationToken) =>
        {
            await executar(comandos, id, Versoes.IfMatch(requisicao), corpo?.Tarefas, cancellationToken);
            return Results.Ok(await DetalheAsync(consultas, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.ProjetoGerenciar)
            .Produces<ProjetoDto>();

    private static async Task<ProjetoDto> DetalheAsync(IConsultasProjeto consultas, Guid id, CancellationToken cancellationToken) =>
        await consultas.DetalharAsync(id, cancellationToken) ?? throw new NaoEncontradoException("Projeto não encontrado.");
}
