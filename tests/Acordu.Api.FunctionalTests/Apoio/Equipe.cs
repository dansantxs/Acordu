using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Api.FunctionalTests.Apoio;

/// <summary>
/// Um tenant novo (registrado pela API) e pessoas da equipe nele, cada uma com seu token. Faz as chamadas HTTP com o
/// Bearer e, quando pedido, o <c>If-Match</c>.
/// </summary>
internal sealed class Equipe
{
    public const string Senha = "senha-bem-comprida";

    private readonly ApiFixture _api;

    private Equipe(ApiFixture api, Guid tenantId, string tokenDono)
    {
        _api = api;
        TenantId = tenantId;
        Dono = tokenDono;
    }

    public Guid TenantId { get; }

    /// <summary>Token de quem registrou o tenant (papel Dono).</summary>
    public string Dono { get; }

    /// <param name="confirmar">
    /// Confirma o e-mail de quem registrou, liberando o envio a terceiros (RF-AUT-06). Falso só nos testes do bloqueio.
    /// </param>
    public static async Task<Equipe> CriarAsync(ApiFixture api, bool confirmar = true)
    {
        var (token, _, email) = await RegistrarAsync(api, "Estúdio Traço");
        if (confirmar)
        {
            using var confirmacao = await EnviarAsync(
                api, HttpMethod.Post, "/api/v1/auth/confirmacao-email", new { token = api.Emails.TokenDe(email, "confirmacao-email") }, null, null);
            confirmacao.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        using var eu = await GetAsync(api, "/api/v1/auth/eu", token);
        var tenant = (await JsonAsync(eu)).GetProperty("tenant").GetProperty("id").GetGuid();
        return new Equipe(api, tenant, token);
    }

    /// <summary>Uma pessoa nova com o papel padrão no tenant (o convite chega depois; aqui o vínculo é gravado direto).</summary>
    public async Task<string> ComPapelAsync(string papel)
    {
        var (token, refresh, _) = await RegistrarAsync(_api, "Própria");
        using var eu = await GetAsync(_api, "/api/v1/auth/eu", token);
        var usuario = (await JsonAsync(eu)).GetProperty("usuarioId").GetGuid();
        await using (var db = _api.DaEquipe(TenantId))
        {
            var alvo = (await db.Papeis.ToListAsync()).Single(p => p.Nome == papel);
            db.Add(Vinculo.Criar(Guid.NewGuid(), usuario, alvo));
            await db.SaveChangesAsync();
        }

        using var cliente = _api.NovoHttp();
        using var troca = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v1/auth/tenant-ativo", UriKind.Relative))
        {
            Content = JsonContent.Create(new { tenantId = TenantId }),
        };
        troca.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        troca.Headers.Add("Cookie", $"acordu_refresh={refresh}");
        using var resposta = await cliente.SendAsync(troca);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await JsonAsync(resposta)).GetProperty("accessToken").GetString()!;
    }

    public Task<HttpResponseMessage> GetAsync(string rota, string? token = null) => GetAsync(_api, rota, token ?? Dono);

    public Task<HttpResponseMessage> EnviarAsync(
        HttpMethod metodo, string rota, object? corpo = null, string? token = null, string? ifMatch = null) =>
        EnviarAsync(_api, metodo, rota, corpo, token ?? Dono, ifMatch);

    public static async Task<JsonElement> JsonAsync(HttpResponseMessage resposta) =>
        await resposta.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>Envia e já devolve o JSON, conferindo o status esperado.</summary>
    public async Task<JsonElement> EsperarAsync(
        HttpStatusCode status, HttpMethod metodo, string rota, object? corpo = null, string? token = null, string? ifMatch = null)
    {
        using var resposta = await EnviarAsync(metodo, rota, corpo, token, ifMatch);
        var texto = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(status, "corpo: {0}", texto);
        return texto.Length == 0 ? default : JsonDocument.Parse(texto).RootElement.Clone();
    }

    private static async Task<(string Token, string Refresh, string Email)> RegistrarAsync(ApiFixture api, string empresa)
    {
        var email = $"pessoa-{Guid.NewGuid():N}@exemplo.com";
        using var resposta = await EnviarAsync(api, HttpMethod.Post, "/api/v1/auth/registro", new
        {
            nome = "Pessoa da equipe",
            email,
            senha = Senha,
            nomeEmpresa = empresa,
        }, null, null);
        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        var cookie = resposta.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("acordu_refresh=", StringComparison.Ordinal));
        return ((await JsonAsync(resposta)).GetProperty("accessToken").GetString()!, cookie.Split(';')[0]["acordu_refresh=".Length..], email);
    }

    private static async Task<HttpResponseMessage> GetAsync(ApiFixture api, string rota, string? token) =>
        await EnviarAsync(api, HttpMethod.Get, rota, null, token, null);

    private static async Task<HttpResponseMessage> EnviarAsync(
        ApiFixture api, HttpMethod metodo, string rota, object? corpo, string? token, string? ifMatch)
    {
        using var cliente = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(metodo, new Uri(rota, UriKind.Relative));
        if (corpo is not null)
            requisicao.Content = JsonContent.Create(corpo);
        if (token is not null)
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (ifMatch is not null)
            requisicao.Headers.TryAddWithoutValidation("If-Match", $"\"{ifMatch}\"");
        return await cliente.SendAsync(requisicao);
    }
}
