using Acordu.Api.Erros;
using Acordu.Application.Abstractions;
using Acordu.Application.Autenticacao;
using Acordu.Application.Cadastro;
using Acordu.Application.Common;

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
            RegistroRequest corpo, IVerificacaoHumana humana, IOrigemRequisicao origem, IServicoAutenticacao servico,
            ConfiguracaoJwt jwt, TimeProvider relogio, HttpResponse resposta, CancellationToken cancellationToken) =>
        {
            // RNF-SEG-10: o desafio vem antes de qualquer validação ou gravação (spec do F9, F9-01 a F9-05).
            var verificacao = await humana.VerificarAsync(corpo.TokenTurnstile, origem.Ip, cancellationToken);
            if (verificacao != ResultadoVerificacaoHumana.Aprovada)
            {
                throw ErroDeCampo.Criar(
                    "tokenTurnstile",
                    verificacao == ResultadoVerificacaoHumana.Indisponivel
                        ? "Não deu para conferir agora. Tente de novo em instantes."
                        : "Confirme que você não é um robô e tente de novo.");
            }

            var sessao = await servico.RegistrarAsync(
                corpo.Nome, corpo.Email, corpo.Senha, corpo.NomeEmpresa, corpo.Documento, corpo.VersaoTermos,
                corpo.VersaoPrivacidade, corpo.Convite, cancellationToken);
            return Results.Created("/api/v1/auth/eu", Responder(sessao, jwt, relogio, resposta));
        })
            .RequireRateLimiting(ComposicaoSeguranca.LimiteRegistro)
            .Produces<TokenDeAcesso>(StatusCodes.Status201Created);

        // Como o cadastro abre (design do F14, D11): a tela mostra o formulário ou o pedido de convite. Sem consultar o banco.
        publicas.MapGet("/cadastro", (OpcoesCadastro cadastro) => Results.Ok(new CadastroInfo(cadastro.Modo)))
            .Produces<CadastroInfo>();

        // O convite de empresa do link (F14-46): o e-mail que a tela fixa. Token que não vale → 401, sem distinguir, como os
        // outros convites.
        publicas.MapPost("/registro/convite/consulta", async (
            ConsultaConviteRequest corpo, IServicoAutenticacao servico, CancellationToken cancellationToken) =>
        {
            var convite = await servico.ConsultarConviteEmpresaAsync(corpo.Token, cancellationToken);
            return convite is null
                ? NaoAutenticado("Convite inválido, vencido ou já usado. Peça um novo convite à Nebrys.")
                : Results.Ok(convite);
        })
            .RequireRateLimiting(ComposicaoSeguranca.LimiteAuth)
            .Produces<ConviteEmpresaInfo>();

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
            .RequireRateLimiting(ComposicaoSeguranca.LimiteRenovacao)
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

        publicas.MapPost("/confirmacao-email", async (
            ConfirmacaoEmailRequest corpo, IServicoAutenticacao servico, CancellationToken cancellationToken) =>
            await servico.ConfirmarEmailAsync(corpo.Token, cancellationToken)
                ? Results.NoContent()
                : NaoAutenticado("Link de confirmação inválido, expirado ou já usado. Peça um novo."))
            .RequireRateLimiting(ComposicaoSeguranca.LimiteAuth);

        // Sempre 202: não revela se o e-mail tem conta.
        publicas.MapPost("/senha/esqueci", async (
            EsqueciSenhaRequest corpo, IServicoAutenticacao servico, CancellationToken cancellationToken) =>
        {
            await servico.SolicitarRedefinicaoSenhaAsync(corpo.Email, cancellationToken);
            return Results.Accepted();
        })
            .RequireRateLimiting(ComposicaoSeguranca.LimiteAuth);

        publicas.MapPost("/senha/redefinir", async (
            RedefinicaoSenhaRequest corpo, IServicoAutenticacao servico, HttpResponse resposta, CancellationToken cancellationToken) =>
        {
            if (!await servico.RedefinirSenhaAsync(corpo.Token, corpo.NovaSenha, cancellationToken))
                return NaoAutenticado("Link de redefinição inválido, expirado ou já usado. Peça um novo.");
            CookieRefresh.Limpar(resposta);
            return Results.NoContent();
        })
            .RequireRateLimiting(ComposicaoSeguranca.LimiteAuth);

        publicas.MapPost("/convite/consulta", async (
            ConsultaConviteRequest corpo, IServicoAutenticacao servico, CancellationToken cancellationToken) =>
        {
            var convite = await servico.ConsultarConviteEquipeAsync(corpo.Token, cancellationToken);
            return convite is null ? ConviteInvalido() : Results.Ok(convite);
        })
            .RequireRateLimiting(ComposicaoSeguranca.LimiteAuth)
            .Produces<ConviteEquipeInfo>();

        publicas.MapPost("/convite", async (
            AceiteConviteEquipeRequest corpo, IServicoAutenticacao servico, ConfiguracaoJwt jwt, TimeProvider relogio,
            HttpResponse resposta, CancellationToken cancellationToken) =>
        {
            var sessao = await servico.AceitarConviteEquipeAsync(
                corpo.Token, corpo.Nome, corpo.Senha, corpo.VersaoTermos, corpo.VersaoPrivacidade, cancellationToken);
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

        auth.MapPost("/confirmacao-email/reenvio", async (
            ITenantContext contexto, IServicoAutenticacao servico, CancellationToken cancellationToken) =>
        {
            await servico.ReenviarConfirmacaoAsync(contexto.UsuarioId!.Value, cancellationToken);
            return Results.Accepted();
        })
            .RequireAuthorization(Politicas.Usuario)
            .RequireRateLimiting(ComposicaoSeguranca.LimiteAuth);

        auth.MapGet("/eu", async (ITenantContext contexto, IServicoAutenticacao servico, CancellationToken cancellationToken) =>
        {
            var perfil = await servico.PerfilAsync(contexto.UsuarioId!.Value, contexto.TenantId!.Value, cancellationToken);
            return perfil is null ? NaoAutenticado("Seu acesso a este tenant foi removido.") : Results.Ok(perfil);
        })
            .RequireAuthorization(Politicas.Usuario)
            .Produces<PerfilUsuario>();

        // "Ciente" dos textos legais (F14-83, F14-119): da conta, só para a equipe (o contato do portal nunca registra
        // aceite, F14-120). Sem corpo: grava a versão vigente de cada texto pendente. Não publicados → 404 (F14-118, D16);
        // a rota fica mapeada, quem recusa é o caso de uso.
        auth.MapPost("/textos/ciente", async (ITenantContext contexto, IServicoAutenticacao servico, CancellationToken cancellationToken) =>
        {
            await servico.RegistrarCienteAsync(contexto.UsuarioId!.Value, cancellationToken);
            return Results.NoContent();
        })
            .RequireAuthorization(Politicas.Usuario, Politicas.Equipe)
            .Produces(StatusCodes.Status204NoContent);
    }

    private static TokenDeAcesso Responder(Sessao sessao, ConfiguracaoJwt jwt, TimeProvider relogio, HttpResponse resposta)
    {
        CookieRefresh.Definir(resposta, sessao.RefreshToken, sessao.RefreshExpiraEm);
        return EmissorJwt.Emitir(sessao, jwt, relogio.GetUtcNow());
    }

    private static IResult ConviteInvalido() =>
        NaoAutenticado("Convite inválido, expirado ou já utilizado. Peça um novo convite a quem convidou você.");

    private static IResult NaoAutenticado(string detalhe) =>
        Results.Problem(detalhe, statusCode: StatusCodes.Status401Unauthorized, title: "Não autenticado",
            type: ManipuladorExcecoes.BaseTipos + "nao-autenticado");

    private static IResult SemPermissao(string detalhe) =>
        Results.Problem(detalhe, statusCode: StatusCodes.Status403Forbidden, title: "Sem permissão",
            type: ManipuladorExcecoes.BaseTipos + "sem-permissao");
}
