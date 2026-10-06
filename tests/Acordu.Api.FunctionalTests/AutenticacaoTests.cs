using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Api.FunctionalTests;

/// <summary>Contrato §2 e segurança §4–5: registro, login, refresh com rotação, logout, troca de tenant e superfícies.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class AutenticacaoTests(ApiFixture api)
{
    private const string Senha = "senha-bem-comprida";
    private const string Base = "https://acordu.app.br/erros/";

    private sealed record Sessao(string AccessToken, string? Refresh);

    private static string NovoEmail() => $"pessoa-{Guid.NewGuid():N}@exemplo.com";

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage resposta) =>
        await resposta.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>Valor do cookie de refresh no Set-Cookie (nulo se ausente ou apagado).</summary>
    private static string? CookieRefresh(HttpResponseMessage resposta)
    {
        if (!resposta.Headers.TryGetValues("Set-Cookie", out var cookies))
            return null;
        var cookie = cookies.FirstOrDefault(c => c.StartsWith("acordu_refresh=", StringComparison.Ordinal));
        var valor = cookie?.Split(';')[0]["acordu_refresh=".Length..];
        return string.IsNullOrEmpty(valor) ? null : valor;
    }

    private async Task<HttpResponseMessage> PostAsync(string rota, object? corpo, string? refresh = null, string? token = null)
    {
        using var cliente = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri(rota, UriKind.Relative));
        if (corpo is not null)
            requisicao.Content = JsonContent.Create(corpo);
        if (refresh is not null)
            requisicao.Headers.Add("Cookie", $"acordu_refresh={refresh}");
        if (token is not null)
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await cliente.SendAsync(requisicao);
    }

    private async Task<HttpResponseMessage> GetAsync(string rota, string? token)
    {
        using var cliente = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, new Uri(rota, UriKind.Relative));
        if (token is not null)
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await cliente.SendAsync(requisicao);
    }

    private async Task<Sessao> RegistrarAsync(string email, string empresa = "Estúdio Traço")
    {
        using var resposta = await PostAsync(
            "/api/v1/auth/registro", new { nome = "Ana Souza", email, senha = Senha, nomeEmpresa = empresa });
        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        return new Sessao((await JsonAsync(resposta)).GetProperty("accessToken").GetString()!, CookieRefresh(resposta));
    }

    private async Task<JsonElement> EuAsync(string token)
    {
        using var resposta = await GetAsync("/api/v1/auth/eu", token);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await JsonAsync(resposta);
    }

    /// <summary>Vincula o usuário a outro tenant com um dos papéis padrão (o que convite/aceite farão depois).</summary>
    private async Task VincularAsync(Guid usuarioId, Guid tenantId, string papel, Guid? clienteId = null)
    {
        await using var db = api.DaEquipe(tenantId);
        var alvo = (await db.Papeis.ToListAsync()).Single(p => p.Nome == papel);
        db.Add(clienteId is null
            ? Vinculo.Criar(Guid.NewGuid(), usuarioId, alvo)
            : Vinculo.Criar(Guid.NewGuid(), usuarioId, alvo, clienteId, EscopoPortal.Proprias));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Registro_AbreSessaoComoDonoComCookieProtegido()
    {
        var email = NovoEmail();
        using var resposta = await PostAsync(
            "/api/v1/auth/registro", new { nome = "Ana Souza", email, senha = Senha, nomeEmpresa = "Estúdio Traço" });

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        var cookie = resposta.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("acordu_refresh=", StringComparison.Ordinal));
        cookie.Should().Contain("httponly").And.Contain("secure").And.Contain("samesite=strict").And.Contain("path=/api/v1/auth");
        var corpo = await JsonAsync(resposta);
        corpo.TryGetProperty("refreshToken", out _).Should().BeFalse();

        var eu = await EuAsync(corpo.GetProperty("accessToken").GetString()!);
        eu.GetProperty("email").GetString().Should().Be(email);
        eu.GetProperty("papel").GetString().Should().Be(Papel.NomeDono);
        eu.GetProperty("tenant").GetProperty("nome").GetString().Should().Be("Estúdio Traço");
        eu.GetProperty("tenant").GetProperty("fusoHorario").GetString().Should().Be(CalendarioUtil.FusoPadrao);
        eu.GetProperty("permissoes").EnumerateArray().Select(p => p.GetString()).Should().Contain(Permissoes.ContratoGerenciar);
        eu.GetProperty("tenants").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Registro_EmailRepetidoOuSenhaCurta_400NoCampo()
    {
        var email = NovoEmail();
        await RegistrarAsync(email);

        using var repetido = await PostAsync(
            "/api/v1/auth/registro", new { nome = "Ana", email, senha = Senha, nomeEmpresa = "Outro" });
        using var curta = await PostAsync(
            "/api/v1/auth/registro", new { nome = "Ana", email = NovoEmail(), senha = "curta", nomeEmpresa = "Outro" });

        repetido.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await JsonAsync(repetido)).GetProperty("errors").TryGetProperty("email", out _).Should().BeTrue();
        curta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await JsonAsync(curta)).GetProperty("errors").TryGetProperty("senha", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Login_CredenciaisCertas_AbreSessao_ErradasDao401()
    {
        var email = NovoEmail();
        await RegistrarAsync(email);

        using var certo = await PostAsync("/api/v1/auth/login", new { email, senha = Senha });
        using var errado = await PostAsync("/api/v1/auth/login", new { email, senha = "outra-senha-qualquer" });
        using var inexistente = await PostAsync("/api/v1/auth/login", new { email = NovoEmail(), senha = Senha });

        certo.StatusCode.Should().Be(HttpStatusCode.OK);
        CookieRefresh(certo).Should().NotBeNull();
        errado.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await JsonAsync(errado)).GetProperty("type").GetString().Should().Be(Base + "nao-autenticado");
        inexistente.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_CincoFalhas_BloqueiaMesmoComSenhaCerta()
    {
        var email = NovoEmail();
        await RegistrarAsync(email);

        for (var i = 0; i < 5; i++)
        {
            using var falha = await PostAsync("/api/v1/auth/login", new { email, senha = "outra-senha-qualquer" });
        }

        using var certo = await PostAsync("/api/v1/auth/login", new { email, senha = Senha });

        certo.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_Rotaciona_EReusoDerrubaASessao()
    {
        var sessao = await RegistrarAsync(NovoEmail());

        using var primeira = await PostAsync("/api/v1/auth/refresh", null, sessao.Refresh);
        var novo = CookieRefresh(primeira);
        using var reuso = await PostAsync("/api/v1/auth/refresh", null, sessao.Refresh);
        using var depoisDoReuso = await PostAsync("/api/v1/auth/refresh", null, novo);

        primeira.StatusCode.Should().Be(HttpStatusCode.OK);
        novo.Should().NotBeNull().And.NotBe(sessao.Refresh);
        (await JsonAsync(primeira)).GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        reuso.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        depoisDoReuso.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_SemCookie_401()
    {
        using var resposta = await PostAsync("/api/v1/auth/refresh", null);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_RevogaORefresh()
    {
        var sessao = await RegistrarAsync(NovoEmail());

        using var logout = await PostAsync("/api/v1/auth/logout", null, sessao.Refresh);
        using var refresh = await PostAsync("/api/v1/auth/refresh", null, sessao.Refresh);

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RotaInterna_ContextoVemDoToken()
    {
        var sessao = await RegistrarAsync(NovoEmail());
        var eu = await EuAsync(sessao.AccessToken);

        using var resposta = await GetAsync("/api/v1/teste-auth/contexto", sessao.AccessToken);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var contexto = await JsonAsync(resposta);
        contexto.GetProperty("tenantId").GetGuid().Should().Be(eu.GetProperty("tenant").GetProperty("id").GetGuid());
        contexto.GetProperty("usuarioId").GetGuid().Should().Be(eu.GetProperty("usuarioId").GetGuid());
        contexto.GetProperty("papel").GetString().Should().Be(Papel.NomeDono);
        contexto.GetProperty("ehCliente").GetBoolean().Should().BeFalse();
        contexto.GetProperty("permissoes").GetInt32().Should().Be(Permissoes.Internas.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("nao-e-um-jwt")]
    public async Task RotaInterna_SemTokenValido_401EmProblemDetails(string? token)
    {
        using var resposta = await GetAsync("/api/v1/teste-auth/contexto", token);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await JsonAsync(resposta)).GetProperty("type").GetString().Should().Be(Base + "nao-autenticado");
    }

    [Fact]
    public async Task TokenExpirado_401()
    {
        var sessao = await RegistrarAsync(NovoEmail());

        api.Relogio.Agora = api.Relogio.Agora.AddMinutes(61);
        using var resposta = await GetAsync("/api/v1/teste-auth/contexto", sessao.AccessToken);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Cliente_403NaRotaInterna_200NoPortal_ETrocaDeTenant()
    {
        var prestador = await RegistrarAsync(NovoEmail(), "Agência");
        var tenantPrestador = (await EuAsync(prestador.AccessToken)).GetProperty("tenant").GetProperty("id").GetGuid();
        var contato = await RegistrarAsync(NovoEmail(), "Padaria");
        var usuarioContato = (await EuAsync(contato.AccessToken)).GetProperty("usuarioId").GetGuid();
        var clienteId = Guid.NewGuid();
        await VincularAsync(usuarioContato, tenantPrestador, Papel.NomeCliente, clienteId);

        using var troca = await PostAsync(
            "/api/v1/auth/tenant-ativo", new { tenantId = tenantPrestador }, contato.Refresh, contato.AccessToken);
        troca.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenCliente = (await JsonAsync(troca)).GetProperty("accessToken").GetString()!;

        using var interna = await GetAsync("/api/v1/teste-auth/contexto", tokenCliente);
        using var portal = await GetAsync("/api/v1/portal/teste-auth", tokenCliente);
        var eu = await EuAsync(tokenCliente);
        using var refreshAntigo = await PostAsync("/api/v1/auth/refresh", null, contato.Refresh);

        interna.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await JsonAsync(interna)).GetProperty("type").GetString().Should().Be(Base + "sem-permissao");
        portal.StatusCode.Should().Be(HttpStatusCode.OK);
        var doPortal = await JsonAsync(portal);
        doPortal.GetProperty("clienteId").GetGuid().Should().Be(clienteId);
        doPortal.GetProperty("escopoPortal").GetString().Should().Be("PROPRIAS");
        eu.GetProperty("papel").GetString().Should().Be(Papel.NomeCliente);
        eu.GetProperty("clienteId").GetGuid().Should().Be(clienteId);
        eu.GetProperty("tenants").GetArrayLength().Should().Be(2);
        refreshAntigo.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "a troca de tenant revoga o refresh anterior");
    }

    [Fact]
    public async Task Equipe_NoPortal_403()
    {
        var sessao = await RegistrarAsync(NovoEmail());

        using var resposta = await GetAsync("/api/v1/portal/teste-auth", sessao.AccessToken);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TrocaParaTenantSemVinculo_403()
    {
        var sessao = await RegistrarAsync(NovoEmail());

        using var resposta = await PostAsync(
            "/api/v1/auth/tenant-ativo", new { tenantId = Guid.NewGuid() }, sessao.Refresh, sessao.AccessToken);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Permissao_LeitorSemContratoGerenciar_403_Dono200()
    {
        var dono = await RegistrarAsync(NovoEmail(), "Agência");
        var tenant = (await EuAsync(dono.AccessToken)).GetProperty("tenant").GetProperty("id").GetGuid();
        var leitor = await RegistrarAsync(NovoEmail(), "Outra");
        var usuarioLeitor = (await EuAsync(leitor.AccessToken)).GetProperty("usuarioId").GetGuid();
        await VincularAsync(usuarioLeitor, tenant, Papel.NomeLeitor);
        using var troca = await PostAsync("/api/v1/auth/tenant-ativo", new { tenantId = tenant }, leitor.Refresh, leitor.AccessToken);
        var tokenLeitor = (await JsonAsync(troca)).GetProperty("accessToken").GetString()!;

        using var comoLeitor = await GetAsync("/api/v1/teste-auth/contratos", tokenLeitor);
        using var comoDono = await GetAsync("/api/v1/teste-auth/contratos", dono.AccessToken);

        comoLeitor.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        comoDono.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_VoltaParaOUltimoTenantUsado()
    {
        var dono = await RegistrarAsync(NovoEmail(), "Agência");
        var tenant = (await EuAsync(dono.AccessToken)).GetProperty("tenant").GetProperty("id").GetGuid();
        var email = NovoEmail();
        var membro = await RegistrarAsync(email, "Própria");
        await VincularAsync((await EuAsync(membro.AccessToken)).GetProperty("usuarioId").GetGuid(), tenant, Papel.NomeMembro);
        using var troca = await PostAsync("/api/v1/auth/tenant-ativo", new { tenantId = tenant }, membro.Refresh, membro.AccessToken);

        using var login = await PostAsync("/api/v1/auth/login", new { email, senha = Senha });

        var eu = await EuAsync((await JsonAsync(login)).GetProperty("accessToken").GetString()!);
        eu.GetProperty("tenant").GetProperty("id").GetGuid().Should().Be(tenant);
        eu.GetProperty("papel").GetString().Should().Be(Papel.NomeMembro);
    }
}
