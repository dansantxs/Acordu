using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Entities;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Contrato §2 e §4.2, segurança §3.3–4: convite do contato para o portal, aceite (conta nova ou existente), escopo e
/// revogação (inclusive ao inativar contato ou cliente).
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class PortalAcessosTests(ApiFixture api)
{
    private const string Senha = "senha-do-contato-123";

    private static string NovoEmail() => $"contato-{Guid.NewGuid():N}@bomgrao.com.br";

    [Fact]
    public async Task Convite_Aceite_AbreSessaoDeClienteComOEscopoDoConvite()
    {
        var equipe = await Equipe.CriarAsync(api);
        var (cliente, contato, email) = await ContatoAsync(equipe);

        var convidado = await equipe.EsperarAsync(
            HttpStatusCode.Accepted, HttpMethod.Post, Rota(cliente, contato), new { escopoPortal = "PROPRIAS" });

        convidado.GetProperty("acessoPortal").GetProperty("situacao").GetString().Should().Be("CONVIDADO");
        convidado.GetProperty("acessoPortal").GetProperty("escopoPortal").GetString().Should().Be("PROPRIAS");
        var emails = api.Emails.Para(email);
        emails.Should().HaveCount(1);
        emails[0].Assunto.Should().Contain("Estúdio Traço");
        var detalhe = await ContatoDoClienteAsync(equipe, cliente, contato);
        detalhe.GetProperty("acessoPortal").GetProperty("conviteExpiraEm").GetDateTimeOffset()
            .Should().Be(api.Relogio.Agora.AddDays(7));

        var token = api.Emails.TokenDoConvite(email);
        using (var consulta = await PostAsync("/api/v1/auth/portal/convite/consulta", new { token }))
        {
            consulta.StatusCode.Should().Be(HttpStatusCode.OK);
            var info = await Equipe.JsonAsync(consulta);
            info.GetProperty("nomePrestador").GetString().Should().Be("Estúdio Traço");
            info.GetProperty("nomeContato").GetString().Should().Be("Marina Souza");
            info.GetProperty("email").GetString().Should().Be(email);
            info.GetProperty("temConta").GetBoolean().Should().BeFalse();
        }

        var sessao = await AceitarAsync(token);

        var portal = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/portal/teste-auth", token: sessao.Token);
        portal.GetProperty("clienteId").GetGuid().Should().Be(cliente);
        portal.GetProperty("escopoPortal").GetString().Should().Be("PROPRIAS");
        var eu = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: sessao.Token);
        eu.GetProperty("papel").GetString().Should().Be(Papel.NomeCliente);
        eu.GetProperty("nome").GetString().Should().Be("Marina Souza");
        var ativo = await ContatoDoClienteAsync(equipe, cliente, contato);
        ativo.GetProperty("temConta").GetBoolean().Should().BeTrue();
        ativo.GetProperty("acessoPortal").GetProperty("situacao").GetString().Should().Be("ATIVO");

        // Papel Cliente em rota interna → 403; o mesmo token não aceita de novo.
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Get, "/api/v1/clientes", token: sessao.Token);
        using var deNovo = await PostAsync("/api/v1/auth/portal/convite", new { token, senha = Senha });
        deNovo.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Aceite_ContaExistente_ExigeASenhaDela_EGanhaOutroTenant()
    {
        var email = NovoEmail();
        using (var registro = await PostAsync("/api/v1/auth/registro", new
        {
            nome = "Marina", email, senha = Equipe.Senha, nomeEmpresa = "Empresa da Marina",
        }))
        {
            registro.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var equipe = await Equipe.CriarAsync(api);
        var (cliente, contato, _) = await ContatoAsync(equipe, email);
        await ConvidarAsync(equipe, cliente, contato);
        var token = api.Emails.TokenDoConvite(email);

        using (var consulta = await PostAsync("/api/v1/auth/portal/convite/consulta", new { token }))
            (await Equipe.JsonAsync(consulta)).GetProperty("temConta").GetBoolean().Should().BeTrue();

        using (var errada = await PostAsync("/api/v1/auth/portal/convite", new { token, senha = "outra-senha-qualquer" }))
        {
            errada.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Equipe.JsonAsync(errada)).GetProperty("errors").TryGetProperty("senha", out _).Should().BeTrue();
        }

        var sessao = await AceitarAsync(token, Equipe.Senha);

        var eu = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: sessao.Token);
        eu.GetProperty("tenant").GetProperty("id").GetGuid().Should().Be(equipe.TenantId);
        eu.GetProperty("tenants").GetArrayLength().Should().Be(2);
        eu.GetProperty("nome").GetString().Should().Be("Marina", "a conta existente mantém o próprio nome");
    }

    [Fact]
    public async Task Aceite_SenhaCurta_400_EConviteContinuaValendo()
    {
        var equipe = await Equipe.CriarAsync(api);
        var (cliente, contato, email) = await ContatoAsync(equipe);
        await ConvidarAsync(equipe, cliente, contato);
        var token = api.Emails.TokenDoConvite(email);

        using (var curta = await PostAsync("/api/v1/auth/portal/convite", new { token, senha = "curta" }))
        {
            curta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Equipe.JsonAsync(curta)).GetProperty("errors").TryGetProperty("senha", out _).Should().BeTrue();
        }

        await AceitarAsync(token, nome: "Marina S.");
    }

    [Fact]
    public async Task Aceite_ComBearerDeOutraSessao_IgnoraOToken()
    {
        var equipe = await Equipe.CriarAsync(api);
        var (cliente, contato, email) = await ContatoAsync(equipe);
        await ConvidarAsync(equipe, cliente, contato);

        using var resposta = await PostAsync(
            "/api/v1/auth/portal/convite", new { token = api.Emails.TokenDoConvite(email), senha = Senha }, bearer: equipe.Dono);

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task TokenInvalidoOuExpirado_401SemDistinguir()
    {
        var equipe = await Equipe.CriarAsync(api);
        var (cliente, contato, email) = await ContatoAsync(equipe);
        await ConvidarAsync(equipe, cliente, contato);
        var token = api.Emails.TokenDoConvite(email);

        using (var invalido = await PostAsync("/api/v1/auth/portal/convite/consulta", new { token = "nao-existe" }))
            invalido.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var agora = api.Relogio.Agora;
        api.Relogio.Agora = agora.AddDays(7);
        try
        {
            using var expirado = await PostAsync("/api/v1/auth/portal/convite", new { token, senha = Senha });
            expirado.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        finally
        {
            api.Relogio.Agora = agora;
        }
    }

    [Fact]
    public async Task Reconvidar_InvalidaOConviteAnterior()
    {
        var equipe = await Equipe.CriarAsync(api);
        var (cliente, contato, email) = await ContatoAsync(equipe);
        await ConvidarAsync(equipe, cliente, contato);
        var primeiro = api.Emails.TokenDoConvite(email);

        await ConvidarAsync(equipe, cliente, contato);
        var segundo = api.Emails.TokenDoConvite(email);

        segundo.Should().NotBe(primeiro);
        using (var antigo = await PostAsync("/api/v1/auth/portal/convite/consulta", new { token = primeiro }))
            antigo.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var novo = await PostAsync("/api/v1/auth/portal/convite/consulta", new { token = segundo });
        novo.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Convidar_Recusas()
    {
        var equipe = await Equipe.CriarAsync(api);
        var (cliente, contato, email) = await ContatoAsync(equipe);

        // Escopo obrigatório.
        var semEscopo = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Post, Rota(cliente, contato), new { });
        semEscopo.GetProperty("errors").TryGetProperty("escopoPortal", out _).Should().BeTrue();

        // Quem já tem acesso não é reconvidado.
        await ConvidarAsync(equipe, cliente, contato);
        await AceitarAsync(api.Emails.TokenDoConvite(email));
        await equipe.EsperarAsync(
            HttpStatusCode.UnprocessableEntity, HttpMethod.Post, Rota(cliente, contato), new { escopoPortal = "PROPRIAS" });

        // E-mail de alguém da equipe deste tenant.
        var eu = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu");
        var (_, daEquipe, _) = await ContatoAsync(equipe, eu.GetProperty("email").GetString()!, cliente);
        await equipe.EsperarAsync(
            HttpStatusCode.UnprocessableEntity, HttpMethod.Post, Rota(cliente, daEquipe), new { escopoPortal = "PROPRIAS" });

        // Sem permissão (Leitor) e de outro tenant.
        var leitor = await equipe.ComPapelAsync(Papel.NomeLeitor);
        await equipe.EsperarAsync(
            HttpStatusCode.Forbidden, HttpMethod.Post, Rota(cliente, contato), new { escopoPortal = "PROPRIAS" }, leitor);
        var outra = await Equipe.CriarAsync(api);
        await outra.EsperarAsync(
            HttpStatusCode.NotFound, HttpMethod.Post, Rota(cliente, contato), new { escopoPortal = "PROPRIAS" });
    }

    [Fact]
    public async Task AlterarEscopo_DoConvitePendente_EDoAcesso_ValeNoProximoRefresh()
    {
        var equipe = await Equipe.CriarAsync(api);
        var (cliente, contato, email) = await ContatoAsync(equipe);

        await equipe.EsperarAsync(
            HttpStatusCode.UnprocessableEntity, HttpMethod.Patch, Rota(cliente, contato), new { escopoPortal = "PROPRIAS" });

        await ConvidarAsync(equipe, cliente, contato, "TODAS_DO_CLIENTE");
        var pendente = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Patch, Rota(cliente, contato), new { escopoPortal = "PROPRIAS" });
        pendente.GetProperty("acessoPortal").GetProperty("situacao").GetString().Should().Be("CONVIDADO");
        pendente.GetProperty("acessoPortal").GetProperty("escopoPortal").GetString().Should().Be("PROPRIAS");

        var sessao = await AceitarAsync(api.Emails.TokenDoConvite(email));
        (await EscopoAsync(equipe, sessao.Token)).Should().Be("PROPRIAS");

        var ativo = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Patch, Rota(cliente, contato), new { escopoPortal = "TODAS_DO_CLIENTE" });
        ativo.GetProperty("acessoPortal").GetProperty("situacao").GetString().Should().Be("ATIVO");

        using var refresh = await PostAsync("/api/v1/auth/refresh", null, refresh: sessao.Refresh);
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var novo = (await Equipe.JsonAsync(refresh)).GetProperty("accessToken").GetString()!;
        (await EscopoAsync(equipe, novo)).Should().Be("TODAS_DO_CLIENTE");
    }

    [Fact]
    public async Task Revogar_DerrubaSessoes_EPermiteNovoConvite()
    {
        var equipe = await Equipe.CriarAsync(api);
        var (cliente, contato, email) = await ContatoAsync(equipe);
        await ConvidarAsync(equipe, cliente, contato);
        var sessao = await AceitarAsync(api.Emails.TokenDoConvite(email));

        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, Rota(cliente, contato));

        using (var refresh = await PostAsync("/api/v1/auth/refresh", null, refresh: sessao.Refresh))
            refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await equipe.EsperarAsync(HttpStatusCode.Unauthorized, HttpMethod.Get, "/api/v1/auth/eu", token: sessao.Token);
        var revogado = await ContatoDoClienteAsync(equipe, cliente, contato);
        revogado.GetProperty("temConta").GetBoolean().Should().BeFalse();
        revogado.GetProperty("acessoPortal").ValueKind.Should().Be(JsonValueKind.Null);

        // Idempotente, e o contato pode ser convidado de novo (a conta dele continua existindo).
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, Rota(cliente, contato));
        await ConvidarAsync(equipe, cliente, contato);
        await AceitarAsync(api.Emails.TokenDoConvite(email));
    }

    [Fact]
    public async Task InativarContatoOuCliente_RevogaAcessosEConvites()
    {
        var equipe = await Equipe.CriarAsync(api);
        var (cliente, marina, emailMarina) = await ContatoAsync(equipe);
        var (_, joao, emailJoao) = await ContatoAsync(equipe, NovoEmail(), cliente);
        var (_, ana, emailAna) = await ContatoAsync(equipe, NovoEmail(), cliente);
        await ConvidarAsync(equipe, cliente, marina);
        await ConvidarAsync(equipe, cliente, joao);
        await ConvidarAsync(equipe, cliente, ana);
        var sessaoMarina = await AceitarAsync(api.Emails.TokenDoConvite(emailMarina));
        var sessaoJoao = await AceitarAsync(api.Emails.TokenDoConvite(emailJoao));
        var conviteAna = api.Emails.TokenDoConvite(emailAna);

        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/v1/clientes/{cliente}/contatos/{marina}");

        using (var refreshMarina = await PostAsync("/api/v1/auth/refresh", null, refresh: sessaoMarina.Refresh))
            refreshMarina.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using (var refreshJoao = await PostAsync("/api/v1/auth/refresh", null, refresh: sessaoJoao.Refresh))
        {
            refreshJoao.StatusCode.Should().Be(HttpStatusCode.OK, "inativar um contato não afeta os colegas");
            sessaoJoao = sessaoJoao with { Refresh = CookieRefresh(refreshJoao)! };
        }

        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/v1/clientes/{cliente}");

        using (var refreshJoao = await PostAsync("/api/v1/auth/refresh", null, refresh: sessaoJoao.Refresh))
            refreshJoao.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var consultaAna = await PostAsync("/api/v1/auth/portal/convite/consulta", new { token = conviteAna });
        consultaAna.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string Rota(Guid cliente, Guid contato) => $"/api/v1/clientes/{cliente}/contatos/{contato}/acesso-portal";

    private static async Task<(Guid Cliente, Guid Contato, string Email)> ContatoAsync(
        Equipe equipe, string? email = null, Guid? cliente = null)
    {
        var clienteId = cliente ?? await Cenarios.ClienteAsync(equipe);
        var emailContato = email ?? NovoEmail();
        var contato = await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/clientes/{clienteId}/contatos",
            new { nome = "Marina Souza", email = emailContato });
        return (clienteId, Cenarios.Id(contato), emailContato);
    }

    private static Task<JsonElement> ConvidarAsync(Equipe equipe, Guid cliente, Guid contato, string escopo = "TODAS_DO_CLIENTE") =>
        equipe.EsperarAsync(HttpStatusCode.Accepted, HttpMethod.Post, Rota(cliente, contato), new { escopoPortal = escopo });

    private static async Task<JsonElement> ContatoDoClienteAsync(Equipe equipe, Guid cliente, Guid contato)
    {
        var detalhe = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/clientes/{cliente}");
        return detalhe.GetProperty("contatos").EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == contato);
    }

    private static async Task<string?> EscopoAsync(Equipe equipe, string token) =>
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/portal/teste-auth", token: token))
            .GetProperty("escopoPortal").GetString();

    private async Task<SessaoContato> AceitarAsync(string token, string senha = Senha, string? nome = null)
    {
        using var resposta = await PostAsync("/api/v1/auth/portal/convite", new { token, nome, senha });
        var texto = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.Created, "corpo: {0}", texto);
        var acesso = JsonDocument.Parse(texto).RootElement.GetProperty("accessToken").GetString()!;
        return new SessaoContato(acesso, CookieRefresh(resposta)!);
    }

    private static string? CookieRefresh(HttpResponseMessage resposta) =>
        resposta.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.FirstOrDefault(c => c.StartsWith("acordu_refresh=", StringComparison.Ordinal))?.Split(';')[0]["acordu_refresh=".Length..]
            : null;

    /// <summary>POST sem a sessão da equipe: rotas públicas, refresh por cookie e, se pedido, um Bearer qualquer.</summary>
    private async Task<HttpResponseMessage> PostAsync(string rota, object? corpo, string? refresh = null, string? bearer = null)
    {
        using var http = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri(rota, UriKind.Relative));
        if (corpo is not null)
            requisicao.Content = JsonContent.Create(corpo);
        if (refresh is not null)
            requisicao.Headers.Add("Cookie", $"acordu_refresh={refresh}");
        if (bearer is not null)
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return await http.SendAsync(requisicao);
    }

    private sealed record SessaoContato(string Token, string Refresh);
}
