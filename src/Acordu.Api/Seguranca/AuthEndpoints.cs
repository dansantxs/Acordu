using Acordu.Api.Erros;
using Acordu.Application.Abstractions;
using Acordu.Application.Autenticacao;

namespace Acordu.Api.Seguranca;

/// <summary>
/// Rotas de identidade e sessão (contrato §2). Abrir sessão devolve o access token no corpo e o refresh token em cookie
/// (<see cref="CookieRefresh"/>). Falhas de credencial respondem 401 sem dizer o motivo.
/// </summary>
internal static class AuthEndpoints
{
    public static void MapAuth(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Autenticação");
        // Públicas: não usam um eventual Bearer da requisição (ContextoDaRequisicao).
        var publicas = auth.MapGroup(string.Empty).AllowAnonymous();

        publicas.MapPost("/registro", async (
            RegistroRequest corpo, IServicoAutenticacao servico, ConfiguracaoJwt jwt, TimeProvider relogio, HttpResponse resposta,
            CancellationToken cancellationToken) =>
        {
            var sessao = await servico.RegistrarAsync(corpo.Nome, corpo.Email, corpo.Senha, corpo.NomeEmpresa, cancellationToken);
            return Results.Created("/api/v1/auth/eu", Responder(sessao, jwt, relogio, resposta));
        })
            .RequireRateLimiting(ComposicaoSeguranca.LimiteAuth)
            .Produces<TokenDeAcesso>(StatusCodes.Status201Created);

        publicas.MapPost("/login", async (
            LoginRequest corpo, IServicoAutenticacao servico, ConfiguracaoJwt jwt, TimeProvider relogio, HttpResponse resposta,
            CancellationToken cancellationToken) =>
        {
            var sessao = await servico.EntrarAsync(corpo.Email, corpo.Senha, cancellationToken);
            return sessao is null
                ? NaoAutenticado("E-mail ou senha inválidos, ou acesso bloqueado temporariamente.")
                : Results.Ok(Responder(sessao, jwt, relogio, resposta));
        })
            .RequireRateLimiting(ComposicaoSeguranca.LimiteAuth)
            .Produces<TokenDeAcesso>();

        publicas.MapPost("/refresh", async (
            IServicoAutenticacao servico, ConfiguracaoJwt jwt, TimeProvider relogio, HttpRequest requisicao, HttpResponse resposta,
            CancellationToken cancellationToken) =>
        {
            var token = CookieRefresh.Ler(requisicao);
            var sessao = token is null ? null : await servico.RenovarAsync(token, cancellationToken);
            if (sessao is null)
            {
                CookieRefresh.Limpar(resposta);
                return NaoAutenticado("Sessão expirada. Entre novamente.");
            }

            return Results.Ok(Responder(sessao, jwt, relogio, resposta));
        })
            .RequireRateLimiting(ComposicaoSeguranca.LimiteAuth)
            .Produces<TokenDeAcesso>();

        publicas.MapPost("/logout", async (
            IServicoAutenticacao servico, HttpRequest requisicao, HttpResponse resposta, CancellationToken cancellationToken) =>
        {
            await servico.SairAsync(CookieRefresh.Ler(requisicao), cancellationToken);
            CookieRefresh.Limpar(resposta);
            return Results.NoContent();
        });

        publicas.MapPost("/portal/convite/consulta", async (
            ConsultaConviteRequest corpo, IServicoAutenticacao servico, CancellationToken cancellationToken) =>
        {
            var convite = await servico.ConsultarConviteAsync(corpo.Token, cancellationToken);
            return convite is null ? ConviteInvalido() : Results.Ok(convite);
        })
            .RequireRateLimiting(ComposicaoSeguranca.LimiteAuth)
            .Produces<ConvitePortalInfo>();

        publicas.MapPost("/portal/convite", async (
            AceiteConviteRequest corpo, IServicoAutenticacao servico, ConfiguracaoJwt jwt, TimeProvider relogio,
            HttpResponse resposta, CancellationToken cancellationToken) =>
        {
            var sessao = await servico.AceitarConviteAsync(corpo.Token, corpo.Nome, corpo.Senha, cancellationToken);
            return sessao is null
                ? ConviteInvalido()
                : Results.Created("/api/v1/auth/eu", Responder(sessao, jwt, relogio, resposta));
        })
            .RequireRateLimiting(ComposicaoSeguranca.LimiteAuth)
            .Produces<TokenDeAcesso>(StatusCodes.Status201Created);

        publicas.MapPost("/portal/link", async (
            LinkPortalRequest corpo, IServicoAutenticacao servico, ConfiguracaoJwt jwt, TimeProvider relogio,
            CancellationToken cancellationToken) =>
        {
            var sessao = await servico.TrocarLinkAsync(corpo.Token, cancellationToken);
            if (sessao is null)
                return NaoAutenticado("Link inválido, expirado ou revogado. Peça um novo link a quem atende você.");

            var acesso = EmissorJwt.EmitirLink(sessao, jwt, relogio.GetUtcNow());
            return Results.Ok(new TokenDeLink(acesso.AccessToken, acesso.ExpiraEm, sessao.SolicitacaoId, ClaimsAcordu.TipoLink));
        })
            .RequireRateLimiting(ComposicaoSeguranca.LimiteAuth)
            .Produces<TokenDeLink>();

        auth.MapPost("/tenant-ativo", async (
            TrocaTenantRequest corpo, ITenantContext contexto, IServicoAutenticacao servico, ConfiguracaoJwt jwt,
            TimeProvider relogio, HttpRequest requisicao, HttpResponse resposta, CancellationToken cancellationToken) =>
        {
            var sessao = await servico.TrocarTenantAsync(
                contexto.UsuarioId!.Value, corpo.TenantId, CookieRefresh.Ler(requisicao), cancellationToken);
            return sessao is null
                ? SemPermissao("Você não tem acesso a esse tenant.")
                : Results.Ok(Responder(sessao, jwt, relogio, resposta));
        })
            .RequireAuthorization(Politicas.Usuario)
            .Produces<TokenDeAcesso>();

        auth.MapGet("/eu", async (ITenantContext contexto, IServicoAutenticacao servico, CancellationToken cancellationToken) =>
        {
            var perfil = await servico.PerfilAsync(contexto.UsuarioId!.Value, contexto.TenantId!.Value, cancellationToken);
            return perfil is null ? NaoAutenticado("Seu acesso a este tenant foi removido.") : Results.Ok(perfil);
        })
            .RequireAuthorization(Politicas.Usuario)
            .Produces<PerfilUsuario>();
    }

    private static TokenDeAcesso Responder(Sessao sessao, ConfiguracaoJwt jwt, TimeProvider relogio, HttpResponse resposta)
    {
        CookieRefresh.Definir(resposta, sessao.RefreshToken, sessao.RefreshExpiraEm);
        return EmissorJwt.Emitir(sessao, jwt, relogio.GetUtcNow());
    }

    private static IResult ConviteInvalido() =>
        NaoAutenticado("Convite inválido, expirado ou já utilizado. Peça um novo convite a quem atende você.");

    private static IResult NaoAutenticado(string detalhe) =>
        Results.Problem(detalhe, statusCode: StatusCodes.Status401Unauthorized, title: "Não autenticado",
            type: ManipuladorExcecoes.BaseTipos + "nao-autenticado");

    private static IResult SemPermissao(string detalhe) =>
        Results.Problem(detalhe, statusCode: StatusCodes.Status403Forbidden, title: "Sem permissão",
            type: ManipuladorExcecoes.BaseTipos + "sem-permissao");
}
