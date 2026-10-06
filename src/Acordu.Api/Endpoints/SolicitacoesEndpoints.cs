using Acordu.Api.Seguranca;
using Acordu.Application.Common;
using Acordu.Application.Solicitacoes;
using Acordu.Domain.Acesso;
using Acordu.Domain.Enums;

namespace Acordu.Api.Endpoints;

/// <summary>
/// Solicitações e linha do tempo (contrato §7). Interações só com texto por enquanto; anexos e links de portal entram
/// nos marcos de anexos e do portal.
/// </summary>
internal static class SolicitacoesEndpoints
{
    public static void MapSolicitacoes(this RouteGroupBuilder interno)
    {
        var solicitacoes = interno.MapGroup("/solicitacoes").WithTags("Solicitações");

        solicitacoes.MapGet(string.Empty, async (
            string? status, Guid? clienteId, Guid? responsavelId, string? prioridade, Guid? categoriaId, string? situacaoSla,
            string? busca, DateOnly? abertaDe, DateOnly? abertaAte, string? ordem, int? pagina, int? tamanhoPagina,
            IConsultasSolicitacao consultas, CancellationToken cancellationToken) =>
        {
            var filtro = new FiltroSolicitacoes(
                Status: status?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => EnumsDaApi.Ler<StatusSolicitacao>(s, nameof(status))!.Value)
                    .ToList(),
                ClienteId: clienteId,
                ResponsavelId: responsavelId,
                Prioridade: EnumsDaApi.Ler<Prioridade>(prioridade, nameof(prioridade)),
                CategoriaId: categoriaId,
                SituacaoSla: EnumsDaApi.Ler<SituacaoSla>(situacaoSla, nameof(situacaoSla)),
                Busca: busca,
                AbertaDe: abertaDe,
                AbertaAte: abertaAte,
                Ordem: ordem);
            return Results.Ok(await consultas.ListarAsync(filtro, new Paginacao(pagina, tamanhoPagina), cancellationToken));
        })
            .RequirePermissao(Permissoes.SolicitacaoVer)
            .Produces<ResultadoPaginado<SolicitacaoResumoDto>>();

        solicitacoes.MapGet("/{id:guid}", async (Guid id, IConsultasSolicitacao consultas, CancellationToken cancellationToken) =>
            Results.Ok(await DetalheAsync(consultas, id, cancellationToken)))
            .RequirePermissao(Permissoes.SolicitacaoVer)
            .Produces<SolicitacaoDto>();

        solicitacoes.MapPost(string.Empty, async (
            NovaSolicitacaoRequest corpo, ComandosSolicitacao comandos, IConsultasSolicitacao consultas, CancellationToken cancellationToken) =>
        {
            var id = await comandos.CriarAsync(corpo, cancellationToken);
            return Results.Created($"/api/v1/solicitacoes/{id}", await DetalheAsync(consultas, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.SolicitacaoCriar)
            .Produces<SolicitacaoDto>(StatusCodes.Status201Created);

        solicitacoes.MapPatch("/{id:guid}", async (
            Guid id, EdicaoSolicitacaoRequest corpo, HttpRequest requisicao, ComandosSolicitacao comandos,
            IConsultasSolicitacao consultas, CancellationToken cancellationToken) =>
        {
            await comandos.EditarAsync(id, corpo, Versoes.IfMatch(requisicao), cancellationToken);
            return Results.Ok(await DetalheAsync(consultas, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.SolicitacaoEditar)
            .Produces<SolicitacaoDto>();

        MapTransicao(solicitacoes, "/inicio", (c, id, versao, ct) => c.IniciarAsync(id, versao, ct));
        MapTransicao(solicitacoes, "/aguardando-cliente", (c, id, versao, ct) => c.AguardarClienteAsync(id, versao, ct));
        MapTransicao(solicitacoes, "/aguardando-terceiro", (c, id, versao, ct) => c.AguardarTerceiroAsync(id, versao, ct));
        MapTransicao(solicitacoes, "/retomada", (c, id, versao, ct) => c.RetomarAsync(id, versao, ct));

        solicitacoes.MapPost("/{id:guid}/resolucao", async (
            Guid id, ResolucaoRequest corpo, HttpRequest requisicao, ComandosSolicitacao comandos, IConsultasSolicitacao consultas,
            CancellationToken cancellationToken) =>
        {
            await comandos.ResolverAsync(id, corpo.Solucao, Versoes.IfMatch(requisicao), cancellationToken);
            return Results.Ok(await DetalheAsync(consultas, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.SolicitacaoEditar)
            .Produces<SolicitacaoDto>();

        solicitacoes.MapPost("/{id:guid}/reabertura", async (
            Guid id, MotivoRequest corpo, HttpRequest requisicao, ComandosSolicitacao comandos, IConsultasSolicitacao consultas,
            CancellationToken cancellationToken) =>
        {
            await comandos.ReabrirAsync(id, corpo.Motivo, Versoes.IfMatch(requisicao), cancellationToken);
            return Results.Ok(await DetalheAsync(consultas, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.SolicitacaoEditar)
            .Produces<SolicitacaoDto>();

        solicitacoes.MapPost("/{id:guid}/cancelamento", async (
            Guid id, MotivoRequest corpo, HttpRequest requisicao, ComandosSolicitacao comandos, IConsultasSolicitacao consultas,
            CancellationToken cancellationToken) =>
        {
            await comandos.CancelarAsync(id, corpo.Motivo, Versoes.IfMatch(requisicao), cancellationToken);
            return Results.Ok(await DetalheAsync(consultas, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.SolicitacaoEditar)
            .Produces<SolicitacaoDto>();

        solicitacoes.MapGet("/{id:guid}/interacoes", async (Guid id, IConsultasSolicitacao consultas, CancellationToken cancellationToken) =>
            Results.Ok(await consultas.InteracoesAsync(id, cancellationToken) ?? throw new NaoEncontradoException("Solicitação não encontrada.")))
            .RequirePermissao(Permissoes.SolicitacaoVer)
            .Produces<IReadOnlyList<InteracaoDto>>();

        solicitacoes.MapPost("/{id:guid}/interacoes", async (
            Guid id, InteracaoRequest corpo, ComandosSolicitacao comandos, IConsultasSolicitacao consultas,
            CancellationToken cancellationToken) =>
        {
            var interacaoId = await comandos.ComentarAsync(id, corpo, cancellationToken);
            var linha = await consultas.InteracoesAsync(id, cancellationToken);
            return Results.Created($"/api/v1/solicitacoes/{id}/interacoes", linha!.Single(i => i.Id == interacaoId));
        })
            .RequirePermissao(Permissoes.SolicitacaoComentar)
            .Produces<InteracaoDto>(StatusCodes.Status201Created);

        solicitacoes.MapGet("/{id:guid}/semelhantes", async (Guid id, IConsultaSemelhantes consultas, CancellationToken cancellationToken) =>
            Results.Ok(await consultas.SemelhantesAsync(id, cancellationToken) ?? throw new NaoEncontradoException("Solicitação não encontrada.")))
            .RequirePermissao(Permissoes.SolicitacaoVer)
            .Produces<IReadOnlyList<SolicitacaoSemelhanteDto>>();
    }

    /// <summary>Ação de status sem corpo (convenções §4): <c>If-Match</c> opcional, devolve a solicitação atualizada.</summary>
    private static void MapTransicao(
        RouteGroupBuilder solicitacoes, string acao, Func<ComandosSolicitacao, Guid, string?, CancellationToken, Task> executar) =>
        solicitacoes.MapPost("/{id:guid}" + acao, async (
            Guid id, HttpRequest requisicao, ComandosSolicitacao comandos, IConsultasSolicitacao consultas, CancellationToken cancellationToken) =>
        {
            await executar(comandos, id, Versoes.IfMatch(requisicao), cancellationToken);
            return Results.Ok(await DetalheAsync(consultas, id, cancellationToken));
        })
            .RequirePermissao(Permissoes.SolicitacaoEditar)
            .Produces<SolicitacaoDto>();

    private static async Task<SolicitacaoDto> DetalheAsync(IConsultasSolicitacao consultas, Guid id, CancellationToken cancellationToken) =>
        await consultas.DetalharAsync(id, cancellationToken) ?? throw new NaoEncontradoException("Solicitação não encontrada.");
}
