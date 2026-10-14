using Acordu.Api.Seguranca;
using Acordu.Application.AcessosPortal;
using Acordu.Application.Anexos;
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

        // Troca ou remove o contrato, com recálculo do SLA (modelo §4.3). Fora do PATCH de propósito.
        solicitacoes.MapPut("/{id:guid}/contrato", async (
            Guid id, TrocaContratoRequest corpo, HttpRequest requisicao, ComandosSolicitacao comandos,
            IConsultasSolicitacao consultas, CancellationToken cancellationToken) =>
        {
            await comandos.TrocarContratoAsync(id, corpo, Versoes.IfMatch(requisicao), cancellationToken);
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

        // JSON { texto, visibilidade } ou multipart (texto, visibilidade, anexos[]) — contrato §7.
        solicitacoes.MapPost("/{id:guid}/interacoes", async (
            Guid id, HttpRequest requisicao, ComandosSolicitacao comandos, IConsultasSolicitacao consultas,
            CancellationToken cancellationToken) =>
        {
            var formulario = await FormularioComAnexos.LerAsync(requisicao, cancellationToken);
            var corpo = formulario is null
                ? await CorpoOpcional.LerAsync<InteracaoRequest>(requisicao, cancellationToken)
                    ?? throw new BadHttpRequestException("Envie o comentário em JSON ou multipart/form-data.")
                : new InteracaoRequest(
                    formulario.Campo("texto"), EnumsDaApi.Ler<Visibilidade>(formulario.Campo("visibilidade"), "visibilidade"));
            var interacaoId = await comandos.ComentarAsync(id, corpo, formulario?.Arquivos, cancellationToken);
            var linha = await consultas.InteracoesAsync(id, cancellationToken);
            return Results.Created($"/api/v1/solicitacoes/{id}/interacoes", linha!.Single(i => i.Id == interacaoId));
        })
            .RequirePermissao(Permissoes.SolicitacaoComentar)
            .Accepts<InteracaoRequest>("application/json", "multipart/form-data")
            .Produces<InteracaoDto>(StatusCodes.Status201Created);

        // Links de acesso sem senha (RF-POR-08). O link (com o token) só aparece na resposta da emissão.
        solicitacoes.MapPost("/{id:guid}/acessos-portal", async (
            Guid id, EmissaoLinkRequest corpo, ComandosLinkPortal comandos, CancellationToken cancellationToken) =>
        {
            var emitido = await comandos.EmitirAsync(id, corpo, cancellationToken);
            return Results.Created($"/api/v1/solicitacoes/{id}/acessos-portal", emitido);
        })
            .RequirePermissao(Permissoes.PortalGerenciarAcessos)
            .Produces<LinkEmitidoDto>(StatusCodes.Status201Created);

        solicitacoes.MapGet("/{id:guid}/acessos-portal", async (
            Guid id, IConsultasLinkPortal consultas, TimeProvider relogio, CancellationToken cancellationToken) =>
            Results.Ok(await consultas.ListarAsync(id, relogio.GetUtcNow(), cancellationToken)
                ?? throw new NaoEncontradoException("Solicitação não encontrada.")))
            .RequirePermissao(Permissoes.PortalGerenciarAcessos)
            .Produces<IReadOnlyList<LinkPortalDto>>();

        solicitacoes.MapDelete("/{id:guid}/acessos-portal/{acessoId:guid}", async (
            Guid id, Guid acessoId, ComandosLinkPortal comandos, CancellationToken cancellationToken) =>
        {
            await comandos.RevogarAsync(id, acessoId, cancellationToken);
            return Results.NoContent();
        })
            .RequirePermissao(Permissoes.PortalGerenciarAcessos);

        // Anexo de interação (interna ou externa) ou de comentário de tarefa: 302 para a URL assinada de 5 minutos (segurança §7).
        interno.MapGet("/anexos/{id:guid}/download", async (Guid id, DownloadAnexos download, CancellationToken cancellationToken) =>
            Results.Redirect((await download.UrlAsync(id, cancellationToken)).ToString()))
            .WithTags("Solicitações")
            .RequireAlgumaPermissao(Permissoes.SolicitacaoVer, Permissoes.TarefaVer)
            .Produces(StatusCodes.Status302Found);

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
