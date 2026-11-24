using System.Text.Json;
using Acordu.Api.Seguranca;
using Acordu.Application.Common;
using Acordu.Application.Nebrys;
using Acordu.Domain.Enums;

namespace Acordu.Api.Endpoints;

/// <summary>
/// Área da Nebrys (contrato §17, ADR 0014): sessão de operação sem tenant, lista e detalhe das empresas, só com os campos
/// da relação com a Nebrys (RNF-PRIV-01), ações sobre a situação delas e liberação de documento (F14l), e convites de
/// empresa (F14n) e a lista de espera (F14o). Quem não tem a sessão de operação recebe <c>404</c> antes de chegar aqui
/// (<see cref="AreaNebrys"/>).
/// Documento OpenAPI próprio (<c>openapi/nebrys.json</c>).
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
            Results.Ok(await DetalheAsync(consultas, id, cancellationToken)))
            .Produces<EmpresaNebrysDto>();

        // Ações sobre a situação (F14l, design D5): If-Match com a versão do detalhe; devolvem o detalhe com a versão nova.
        MapAcao(area, "/ativacao", (acoes, id, versao, ct) => acoes.AtivarAsync(id, versao, ct));
        area.MapPost("/empresas/{id:guid}/prorrogacao", async (
            Guid id, ProrrogacaoTesteRequest corpo, HttpRequest requisicao, AcoesEmpresasNebrys acoes, IConsultasNebrys consultas,
            CancellationToken cancellationToken) =>
        {
            await acoes.ProrrogarAsync(id, corpo, Versoes.IfMatch(requisicao), cancellationToken);
            return Results.Ok(await DetalheAsync(consultas, id, cancellationToken));
        })
            .Produces<EmpresaNebrysDto>();
        MapAcao(area, "/cancelamento", (acoes, id, versao, ct) => acoes.CancelarAsync(id, versao, ct));
        MapAcao(area, "/reativacao", (acoes, id, versao, ct) => acoes.ReativarAsync(id, versao, ct));

        // Liberação de documento (F14-71, F14-108): o documento digitado não é guardado nem vai para a trilha.
        area.MapPost("/documentos/liberacao", async (
            LiberacaoDocumentoRequest corpo, LiberacaoDocumentos liberacao, CancellationToken cancellationToken) =>
        {
            await liberacao.LiberarAsync(corpo, cancellationToken);
            return Results.NoContent();
        })
            .Produces(StatusCodes.Status204NoContent);

        // Convites de empresa (F14-72 a F14-74, design D12): o e-mail sai na requisição, depois do commit; a falha volta em
        // emailEnviado. Nunca o token na resposta.
        area.MapGet("/convites", async (
            int? pagina, int? tamanhoPagina, IConsultasNebrys consultas, CancellationToken cancellationToken) =>
            Results.Ok(await consultas.ListarConvitesAsync(new Paginacao(pagina, tamanhoPagina), cancellationToken)))
            .Produces<ResultadoPaginado<ConviteEmpresaNebrysDto>>();

        area.MapGet("/convites/{id:guid}", async (Guid id, IConsultasNebrys consultas, CancellationToken cancellationToken) =>
            Results.Ok(await ConviteAsync(consultas, id, cancellationToken)))
            .Produces<ConviteEmpresaNebrysDto>();

        area.MapPost("/convites", async (
            ConviteEmpresaRequest corpo, ConvitesEmpresaNebrys convites, IConsultasNebrys consultas, CancellationToken cancellationToken) =>
        {
            var enviado = await convites.EmitirAsync(corpo, cancellationToken);
            return Results.Created(
                $"/api/v1/nebrys/convites/{enviado.Id}",
                new EnvioConviteEmpresaDto(await ConviteAsync(consultas, enviado.Id, cancellationToken), enviado.EmailEnviado));
        })
            .Produces<EnvioConviteEmpresaDto>(StatusCodes.Status201Created);

        area.MapPost("/convites/{id:guid}/reenvio", async (
            Guid id, ConvitesEmpresaNebrys convites, IConsultasNebrys consultas, CancellationToken cancellationToken) =>
        {
            var enviado = await convites.ReenviarAsync(id, cancellationToken);
            return Results.Ok(new EnvioConviteEmpresaDto(await ConviteAsync(consultas, id, cancellationToken), enviado.EmailEnviado));
        })
            .Produces<EnvioConviteEmpresaDto>();

        area.MapPost("/convites/{id:guid}/revogacao", async (
            Guid id, ConvitesEmpresaNebrys convites, IConsultasNebrys consultas, CancellationToken cancellationToken) =>
        {
            await convites.RevogarAsync(id, cancellationToken);
            return Results.Ok(await ConviteAsync(consultas, id, cancellationToken));
        })
            .Produces<ConviteEmpresaNebrysDto>();

        // Lista de espera (F14-75 a F14-77, design D12): por ordem de chegada; convidar emite um convite de empresa para o
        // e-mail da entrada (a mesma resposta da emissão), e descartar apaga a entrada.
        area.MapGet("/lista-espera", async (
            int? pagina, int? tamanhoPagina, IConsultasNebrys consultas, CancellationToken cancellationToken) =>
            Results.Ok(await consultas.ListarListaEsperaAsync(new Paginacao(pagina, tamanhoPagina), cancellationToken)))
            .Produces<ResultadoPaginado<EntradaListaEsperaNebrysDto>>();

        area.MapPost("/lista-espera/{id:guid}/convite", async (
            Guid id, ListaEsperaNebrys lista, IConsultasNebrys consultas, CancellationToken cancellationToken) =>
        {
            var enviado = await lista.ConvidarAsync(id, cancellationToken);
            return Results.Created(
                $"/api/v1/nebrys/convites/{enviado.Id}",
                new EnvioConviteEmpresaDto(await ConviteAsync(consultas, enviado.Id, cancellationToken), enviado.EmailEnviado));
        })
            .Produces<EnvioConviteEmpresaDto>(StatusCodes.Status201Created);

        area.MapDelete("/lista-espera/{id:guid}", async (Guid id, ListaEsperaNebrys lista, CancellationToken cancellationToken) =>
        {
            await lista.DescartarAsync(id, cancellationToken);
            return Results.NoContent();
        })
            .Produces(StatusCodes.Status204NoContent);
    }

    private static async Task<ConviteEmpresaNebrysDto> ConviteAsync(IConsultasNebrys consultas, Guid id, CancellationToken cancellationToken) =>
        await consultas.ObterConviteAsync(id, cancellationToken) ?? throw new NaoEncontradoException("Convite não encontrado.");

    /// <summary>Ação de situação sem corpo: <c>If-Match</c> opcional (D5), devolve o detalhe atualizado.</summary>
    private static void MapAcao(
        RouteGroupBuilder area,
        string acao,
        Func<AcoesEmpresasNebrys, Guid, string?, CancellationToken, Task> executar) =>
        area.MapPost("/empresas/{id:guid}" + acao, async (
            Guid id, HttpRequest requisicao, AcoesEmpresasNebrys acoes, IConsultasNebrys consultas, CancellationToken cancellationToken) =>
        {
            await executar(acoes, id, Versoes.IfMatch(requisicao), cancellationToken);
            return Results.Ok(await DetalheAsync(consultas, id, cancellationToken));
        })
            .Produces<EmpresaNebrysDto>();

    private static async Task<EmpresaNebrysDto> DetalheAsync(IConsultasNebrys consultas, Guid id, CancellationToken cancellationToken) =>
        await consultas.ObterEmpresaAsync(id, cancellationToken) ?? throw new NaoEncontradoException("Empresa não encontrada.");

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
