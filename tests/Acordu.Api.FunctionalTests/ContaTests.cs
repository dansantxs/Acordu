using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;

namespace Acordu.Api.FunctionalTests;

/// <summary>Contrato §2 e segurança §4: confirmação de e-mail (não bloqueia o login) e redefinição de senha.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class ContaTests(ApiFixture api)
{
    private const string Senha = "senha-bem-comprida";
    private const string NovaSenha = "outra-senha-bem-comprida";

    [Fact]
    public async Task Registro_EnviaConfirmacao_EConfirmarMarcaOEmail()
    {
        var (email, sessao) = await RegistrarAsync();

        (await EuAsync(sessao.Token)).GetProperty("emailConfirmado").GetBoolean().Should().BeFalse("o login não exige confirmação");
        var token = api.Emails.TokenDe(email, "confirmacao-email");

        (await PostAsync("/api/v1/auth/confirmacao-email", new { token })).Should().Be(HttpStatusCode.NoContent);

        (await EuAsync(sessao.Token)).GetProperty("emailConfirmado").GetBoolean().Should().BeTrue();
        (await PostAsync("/api/v1/auth/confirmacao-email", new { token })).Should().Be(HttpStatusCode.Unauthorized);
        (await PostAsync("/api/v1/auth/confirmacao-email", new { token = "nao-existe" })).Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Reenvio_InvalidaOLinkAnterior_ENadaFazDepoisDeConfirmado()
    {
        var (email, sessao) = await RegistrarAsync();
        var primeiro = api.Emails.TokenDe(email, "confirmacao-email");

        (await PostAsync("/api/v1/auth/confirmacao-email/reenvio", null, sessao.Token)).Should().Be(HttpStatusCode.Accepted);
        var segundo = api.Emails.TokenDe(email, "confirmacao-email");

        segundo.Should().NotBe(primeiro);
        (await PostAsync("/api/v1/auth/confirmacao-email", new { token = primeiro })).Should().Be(HttpStatusCode.Unauthorized);
        (await PostAsync("/api/v1/auth/confirmacao-email", new { token = segundo })).Should().Be(HttpStatusCode.NoContent);
        var enviados = api.Emails.Para(email).Count;
        (await PostAsync("/api/v1/auth/confirmacao-email/reenvio", null, sessao.Token)).Should().Be(HttpStatusCode.Accepted);
        api.Emails.Para(email).Should().HaveCount(enviados);
        (await PostAsync("/api/v1/auth/confirmacao-email/reenvio", null)).Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RedefinirSenha_TrocaASenha_DerrubaSessoes_EConfirmaOEmail()
    {
        var (email, sessao) = await RegistrarAsync();
        var desconhecido = $"ninguem-{Guid.NewGuid():N}@exemplo.com";

        (await PostAsync("/api/v1/auth/senha/esqueci", new { email = desconhecido })).Should().Be(HttpStatusCode.Accepted);
        api.Emails.Para(desconhecido).Should().BeEmpty();
        (await PostAsync("/api/v1/auth/senha/esqueci", new { email = email.ToUpperInvariant() })).Should().Be(HttpStatusCode.Accepted);
        var token = api.Emails.TokenDe(email, "redefinicao-senha");

        var fraca = await PostCorpoAsync("/api/v1/auth/senha/redefinir", new { token, novaSenha = "curta" });
        fraca.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        fraca.Corpo.GetProperty("errors").TryGetProperty("novaSenha", out _).Should().BeTrue();
        (await PostAsync("/api/v1/auth/senha/redefinir", new { token, novaSenha = NovaSenha })).Should().Be(HttpStatusCode.NoContent);

        (await PostAsync("/api/v1/auth/refresh", null, refresh: sessao.Refresh)).Should().Be(HttpStatusCode.Unauthorized);
        (await PostAsync("/api/v1/auth/login", new { email, senha = Senha })).Should().Be(HttpStatusCode.Unauthorized);
        var login = await PostCorpoAsync("/api/v1/auth/login", new { email, senha = NovaSenha });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        (await EuAsync(login.Corpo.GetProperty("accessToken").GetString()!)).GetProperty("emailConfirmado").GetBoolean().Should().BeTrue();
        (await PostAsync("/api/v1/auth/senha/redefinir", new { token, novaSenha = "mais-uma-senha-comprida" }))
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RedefinirSenha_LinkDeUmaHora_NovoPedidoInvalidaOAnterior_EDesbloqueia()
    {
        var (email, _) = await RegistrarAsync();
        for (var i = 0; i < 5; i++)
            await PostAsync("/api/v1/auth/login", new { email, senha = "senha-errada-qualquer" });
        (await PostAsync("/api/v1/auth/login", new { email, senha = Senha })).Should().Be(HttpStatusCode.Unauthorized, "conta bloqueada");

        await PostAsync("/api/v1/auth/senha/esqueci", new { email });
        var primeiro = api.Emails.TokenDe(email, "redefinicao-senha");
        await PostAsync("/api/v1/auth/senha/esqueci", new { email });
        var segundo = api.Emails.TokenDe(email, "redefinicao-senha");
        (await PostAsync("/api/v1/auth/senha/redefinir", new { token = primeiro, novaSenha = NovaSenha }))
            .Should().Be(HttpStatusCode.Unauthorized);

        var agora = api.Relogio.Agora;
        api.Relogio.Agora = agora.AddHours(1);
        try
        {
            (await PostAsync("/api/v1/auth/senha/redefinir", new { token = segundo, novaSenha = NovaSenha }))
                .Should().Be(HttpStatusCode.Unauthorized);
        }
        finally
        {
            api.Relogio.Agora = agora;
        }

        await PostAsync("/api/v1/auth/senha/esqueci", new { email });
        (await PostAsync("/api/v1/auth/senha/redefinir", new { token = api.Emails.TokenDe(email, "redefinicao-senha"), novaSenha = NovaSenha }))
            .Should().Be(HttpStatusCode.NoContent);
        (await PostAsync("/api/v1/auth/login", new { email, senha = NovaSenha })).Should().Be(HttpStatusCode.OK, "redefinir desbloqueia");
    }

    private async Task<(string Email, Sessao Sessao)> RegistrarAsync()
    {
        var email = $"conta-{Guid.NewGuid():N}@exemplo.com";
        using var http = api.NovoHttp();
        using var resposta = await http.PostAsJsonAsync(new Uri("/api/v1/auth/registro", UriKind.Relative), new
        {
            nome = "Ana Souza", email, senha = Senha, nomeEmpresa = "Estúdio Traço",
        });
        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        var cookie = resposta.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("acordu_refresh=", StringComparison.Ordinal));
        var token = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        return (email, new Sessao(token, cookie.Split(';')[0]["acordu_refresh=".Length..]));
    }

    private async Task<JsonElement> EuAsync(string token)
    {
        using var http = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/auth/eu", UriKind.Relative));
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resposta = await http.SendAsync(requisicao);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await resposta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<HttpStatusCode> PostAsync(string rota, object? corpo, string? token = null, string? refresh = null)
    {
        using var resposta = await EnviarAsync(rota, corpo, token, refresh);
        return resposta.StatusCode;
    }

    private async Task<(HttpStatusCode StatusCode, JsonElement Corpo)> PostCorpoAsync(string rota, object corpo)
    {
        using var resposta = await EnviarAsync(rota, corpo, null, null);
        return (resposta.StatusCode, await resposta.Content.ReadFromJsonAsync<JsonElement>());
    }

    private async Task<HttpResponseMessage> EnviarAsync(string rota, object? corpo, string? token, string? refresh)
    {
        using var http = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri(rota, UriKind.Relative));
        if (corpo is not null)
            requisicao.Content = JsonContent.Create(corpo);
        if (token is not null)
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (refresh is not null)
            requisicao.Headers.Add("Cookie", $"acordu_refresh={refresh}");
        return await http.SendAsync(requisicao);
    }

    private sealed record Sessao(string Token, string Refresh);
}
