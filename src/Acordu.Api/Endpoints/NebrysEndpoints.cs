using System.Text.Json;
using Acordu.Api.Seguranca;
using Acordu.Application.Common;
using Acordu.Application.Nebrys;
using Acordu.Domain.Enums;

namespace Acordu.Api.Endpoints;

/// <summary>
/// Área da Nebrys (contrato §17, ADR 0014): sessão de operação sem tenant, lista e detalhe das empresas, só com os campos
/// da relação com a Nebrys (RNF-PRIV-01). Quem não tem a sessão de operação recebe <c>404</c> antes de chegar aqui
/// (<see cref="AreaNebrys"/>). Documento OpenAPI próprio (<c>openapi/nebrys.json</c>).
/// </summary>
internal static class NebrysEndpoints
{
    public static void MapNebrys(this RouteGroupBuilder api)
    {
        var nebrys = api.MapGroup("/nebrys").WithTags("Nebrys");

        // Abre a sessão de operação: 404 para qualquer recusa (credencial, bloqueio, fora da lista, e-mail não confirmado e
        // até corpo malformado), igual à rota inexistente; o limite das rotas de /auth vale (ADR 0014, item 2).
        nebrys.MapPost("/sessao", async (
            HttpRequest requisicao, IServicoOperacao servico, ConfiguracaoJwt jwt, TimeProvider relogio,
            CancellationToken cancellationToken) =>
        {
            var corpo = await LerCorpoAsync(requisicao, cancellationToken);
            var operador = corpo is null ? null : await servico.AbrirSessaoAsync(corpo.Email, corpo.Senha, cancellationToken);
            if (operador is not { } id)
                return Results.NotFound();
            var acesso = EmissorJwt.EmitirOperacao(id, jwt, relogio.GetUtcNow());
            return Results.Ok(new TokenDeOperacao(acesso.AccessToken, acesso.ExpiraEm, ClaimsAcordu.TipoOperacao));
        })
            .AllowAnonymous()
            .WithMetadata(new AberturaDaSessaoOperacao())
            .RequireRateLimiting(ComposicaoSeguranca.LimiteAuth)
            .Accepts<SessaoOperacaoRequest>("application/json")
            .Produces<TokenDeOperacao>();

        var area = nebrys.MapGroup(string.Empty)
            .RequireAuthorization(Politicas.Operacao)
            .AddEndpointFilter(AreaNebrys.ConferirOperadorAsync);

        area.MapGet("/empresas", async (
            string? situacao, int? pagina, int? tamanhoPagina, IConsultasNebrys consultas, CancellationToken cancellationToken) =>
            Results.Ok(await consultas.ListarEmpresasAsync(
                EnumsDaApi.Ler<SituacaoEmpresa>(situacao, nameof(situacao)), new Paginacao(pagina, tamanhoPagina), cancellationToken)))
            .Produces<ResultadoPaginado<EmpresaResumoNebrysDto>>();

        area.MapGet("/empresas/{id:guid}", async (Guid id, IConsultasNebrys consultas, CancellationToken cancellationToken) =>
            Results.Ok(await consultas.ObterEmpresaAsync(id, cancellationToken)
                ?? throw new NaoEncontradoException("Empresa não encontrada.")))
            .Produces<EmpresaNebrysDto>();
    }

    /// <summary>Corpo da sessão sem o binding padrão: o 400 do corpo malformado revelaria a rota a quem não é operador.</summary>
    private static async Task<SessaoOperacaoRequest?> LerCorpoAsync(HttpRequest requisicao, CancellationToken cancellationToken)
    {
        if (!requisicao.HasJsonContentType())
            return null;
        try
        {
            return await requisicao.ReadFromJsonAsync<SessaoOperacaoRequest>(cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
