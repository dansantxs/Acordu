using Acordu.Api.Seguranca;
using Acordu.Application.Common;
using Acordu.Application.Portal;
using Acordu.Domain.Acesso;
using Acordu.Domain.Enums;

namespace Acordu.Api.Endpoints;

/// <summary>
/// Portal do cliente (contrato §12), no grupo <c>/api/v1/portal</c> (política Portal + <see cref="SessaoPortal"/>).
/// DTOs próprios; solicitação de outro cliente ou fora do escopo → 404. <c>PermitirLink()</c> marca as rotas ✓L.
/// </summary>
internal static class PortalEndpoints
{
    public static void MapPortal(this RouteGroupBuilder portal)
    {
        portal.WithTags("Portal");

        portal.MapGet("/eu", async (LeituraPortal leitura, CancellationToken cancellationToken) =>
            Results.Ok(await leitura.EuAsync(cancellationToken)))
            .PermitirLink()
            .Produces<PortalEuDto>();

        var solicitacoes = portal.MapGroup("/solicitacoes");

        solicitacoes.MapGet(string.Empty, async (
            string? statusExterno, int? pagina, int? tamanhoPagina, LeituraPortal leitura, CancellationToken cancellationToken) =>
        {
            var status = statusExterno?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => EnumsDaApi.Ler<StatusExterno>(s, nameof(statusExterno))!.Value)
                .ToList();
            return Results.Ok(await leitura.ListarAsync(status, new Paginacao(pagina, tamanhoPagina), cancellationToken));
        })
            .RequirePermissao(Permissoes.PortalSolicitacaoVer)
            .Produces<ResultadoPaginado<SolicitacaoPortalResumoDto>>();

        // JSON { titulo, descricao, prioridade } ou multipart (os mesmos campos + anexos) — RF-POR-01.
        solicitacoes.MapPost(string.Empty, async (
            HttpRequest requisicao, ComandosPortal comandos, LeituraPortal leitura, CancellationToken cancellationToken) =>
        {
            var formulario = await FormularioComAnexos.LerAsync(requisicao, cancellationToken);
            var corpo = formulario is null
                ? await CorpoOpcional.LerAsync<NovaSolicitacaoPortalRequest>(requisicao, cancellationToken)
                    ?? throw new BadHttpRequestException("Envie a solicitação em JSON ou multipart/form-data.")
                : new NovaSolicitacaoPortalRequest(
                    formulario.Campo("titulo") ?? string.Empty,
                    formulario.Campo("descricao"),
                    EnumsDaApi.Ler<Prioridade>(formulario.Campo("prioridade"), "prioridade"));
            var id = await comandos.AbrirSolicitacaoAsync(corpo, formulario?.Arquivos, cancellationToken);
            return Results.Created($"/api/v1/portal/solicitacoes/{id}", await leitura.DetalharAsync(id, cancellationToken));
        })
            .RequirePermissao(Permissoes.PortalSolicitacaoCriar)
            .RequireRateLimiting(ComposicaoSeguranca.LimitePortal)
            .Accepts<NovaSolicitacaoPortalRequest>("application/json", "multipart/form-data")
            .Produces<SolicitacaoPortalDto>(StatusCodes.Status201Created);

        solicitacoes.MapGet("/{id:guid}", async (Guid id, LeituraPortal leitura, CancellationToken cancellationToken) =>
            Results.Ok(await leitura.DetalharAsync(id, cancellationToken)))
            .PermitirLink()
            .RequirePermissao(Permissoes.PortalSolicitacaoVer)
            .Produces<SolicitacaoPortalDto>();

        solicitacoes.MapGet("/{id:guid}/interacoes", async (Guid id, LeituraPortal leitura, CancellationToken cancellationToken) =>
            Results.Ok(await leitura.InteracoesAsync(id, cancellationToken)))
            .PermitirLink()
            .RequirePermissao(Permissoes.PortalSolicitacaoVer)
            .Produces<IReadOnlyList<InteracaoPortalDto>>();

        // JSON { texto } ou multipart (texto, anexos[]).
        solicitacoes.MapPost("/{id:guid}/interacoes", async (
            Guid id, HttpRequest requisicao, ComandosPortal comandos, LeituraPortal leitura, CancellationToken cancellationToken) =>
        {
            var formulario = await FormularioComAnexos.LerAsync(requisicao, cancellationToken);
            var corpo = formulario is null
                ? await CorpoOpcional.LerAsync<RespostaPortalRequest>(requisicao, cancellationToken)
                    ?? throw new BadHttpRequestException("Envie a resposta em JSON ou multipart/form-data.")
                : new RespostaPortalRequest(formulario.Campo("texto"));
            var interacaoId = await comandos.ResponderAsync(id, corpo, formulario?.Arquivos, cancellationToken);
            var interacao = (await leitura.InteracoesAsync(id, cancellationToken)).Single(i => i.Id == interacaoId);
            return Results.Created($"/api/v1/portal/solicitacoes/{id}/interacoes", interacao);
        })
            .PermitirLink()
            .RequirePermissao(Permissoes.PortalSolicitacaoComentar)
            .RequireRateLimiting(ComposicaoSeguranca.LimitePortal)
            .Accepts<RespostaPortalRequest>("application/json", "multipart/form-data")
            .Produces<InteracaoPortalDto>(StatusCodes.Status201Created);

        // Só anexos de interações EXTERNAS de solicitações visíveis ao contato; 302 para a URL assinada (5 min).
        portal.MapGet("/anexos/{id:guid}/download", async (Guid id, LeituraPortal leitura, CancellationToken cancellationToken) =>
            Results.Redirect((await leitura.UrlDoAnexoAsync(id, cancellationToken)).ToString()))
            .PermitirLink()
            .RequirePermissao(Permissoes.PortalSolicitacaoVer)
            .Produces(StatusCodes.Status302Found);

        solicitacoes.MapPost("/{id:guid}/aprovacao", async (
            Guid id, ComandosPortal comandos, LeituraPortal leitura, CancellationToken cancellationToken) =>
        {
            await comandos.AprovarAsync(id, cancellationToken);
            return Results.Ok(await leitura.DetalharAsync(id, cancellationToken));
        })
            .PermitirLink()
            .RequirePermissao(Permissoes.PortalSolicitacaoAprovar)
            .Produces<SolicitacaoPortalDto>();

        solicitacoes.MapPost("/{id:guid}/reprovacao", async (
            Guid id, ReprovacaoPortalRequest corpo, ComandosPortal comandos, LeituraPortal leitura, CancellationToken cancellationToken) =>
        {
            await comandos.ReprovarAsync(id, corpo, cancellationToken);
            return Results.Ok(await leitura.DetalharAsync(id, cancellationToken));
        })
            .PermitirLink()
            .RequirePermissao(Permissoes.PortalSolicitacaoAprovar)
            .Produces<SolicitacaoPortalDto>();

        var contratos = portal.MapGroup("/contratos");

        contratos.MapGet(string.Empty, async (LeituraPortal leitura, CancellationToken cancellationToken) =>
            Results.Ok(await leitura.ContratosAsync(cancellationToken)))
            .RequirePermissao(Permissoes.PortalExtratoVer)
            .Produces<IReadOnlyList<ContratoPortalDto>>();

        contratos.MapGet("/{id:guid}/extrato", async (
            Guid id, string? periodo, LeituraPortal leitura, CancellationToken cancellationToken) =>
            Results.Ok(await leitura.ExtratoAsync(
                id, periodo is null ? null : ContratosEndpoints.Mes(periodo), cancellationToken)))
            .RequirePermissao(Permissoes.PortalExtratoVer)
            .Produces<ExtratoPortalDto>();
    }
}
