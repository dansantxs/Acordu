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

        auth.MapPost("/registro", async (
            RegistroRequest corpo, IServicoAutenticacao servico, ConfiguracaoJwt jwt, TimeProvider relogio, HttpResponse resposta,
            CancellationToken cancellationToken) =>
        {
            var sessao = await servico.RegistrarAsync(corpo.Nome, corpo.Email, corpo.Senha, corpo.NomeEmpresa, cancellationToken);
            return Results.Created("/api/v1/auth/eu", Responder(sessao, jwt, relogio, resposta));
        })
            .RequireRateLimiting(ComposicaoSeguranca.LimiteAuth)
            .Produces<TokenDeAcesso>(StatusCodes.Status201Created);

        auth.MapPost("/login", async (
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

        auth.MapPost("/refresh", async (
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

        auth.MapPost("/logout", async (
            IServicoAutenticacao servico, HttpRequest requisicao, HttpResponse resposta, CancellationToken cancellationToken) =>
        {
            await servico.SairAsync(CookieRefresh.Ler(requisicao), cancellationToken);
            CookieRefresh.Limpar(resposta);
            return Results.NoContent();
        });

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
            .RequireAuthorization()
            .Produces<TokenDeAcesso>();

        auth.MapGet("/eu", async (ITenantContext contexto, IServicoAutenticacao servico, CancellationToken cancellationToken) =>
        {
            var perfil = await servico.PerfilAsync(contexto.UsuarioId!.Value, contexto.TenantId!.Value, cancellationToken);
            return perfil is null ? NaoAutenticado("Seu acesso a este tenant foi removido.") : Results.Ok(perfil);
        })
            .RequireAuthorization()
            .Produces<PerfilUsuario>();
    }

    private static TokenDeAcesso Responder(Sessao sessao, ConfiguracaoJwt jwt, TimeProvider relogio, HttpResponse resposta)
    {
        CookieRefresh.Definir(resposta, sessao.RefreshToken, sessao.RefreshExpiraEm);
        return EmissorJwt.Emitir(sessao, jwt, relogio.GetUtcNow());
    }

    private static IResult NaoAutenticado(string detalhe) =>
        Results.Problem(detalhe, statusCode: StatusCodes.Status401Unauthorized, title: "Não autenticado",
            type: ManipuladorExcecoes.BaseTipos + "nao-autenticado");

    private static IResult SemPermissao(string detalhe) =>
        Results.Problem(detalhe, statusCode: StatusCodes.Status403Forbidden, title: "Sem permissão",
            type: ManipuladorExcecoes.BaseTipos + "sem-permissao");
}
