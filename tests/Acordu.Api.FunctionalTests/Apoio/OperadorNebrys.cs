using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Infrastructure.Seguranca;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Api.FunctionalTests.Apoio;

/// <summary>
/// Operador da área da Nebrys nos testes (F14k, ADR 0014): contas da lista <c>Nebrys__Operadores</c> do fixture, criadas
/// direto pelo Identity (sem empresa), e as chamadas da área com o token da sessão de operação.
/// </summary>
internal static class OperadorNebrys
{
    public const string Email = "operacao@nebrys.test";

    /// <summary>Na lista, mas com o e-mail não confirmado: não abre a sessão.</summary>
    public const string EmailSemConfirmar = "operacao-sem-confirmar@nebrys.test";

    public const string Senha = "senha-da-operacao-123";

    /// <summary>Garante a conta (idempotente) e devolve o Id.</summary>
    public static async Task<Guid> ContaAsync(WebApplication app, string email, bool confirmado = true, string nome = "Operação Nebrys")
    {
        await using var escopo = app.Services.CreateAsyncScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        if (await usuarios.FindByEmailAsync(email) is { } existente)
            return existente.Id;
        var usuario = new Usuario(email, nome) { EmailConfirmed = confirmado };
        var criado = await usuarios.CreateAsync(usuario, Senha);
        criado.Succeeded.Should().BeTrue(string.Join(", ", criado.Errors.Select(e => e.Description)));
        return usuario.Id;
    }

    /// <summary>Abre a sessão de operação do operador padrão e devolve o access token.</summary>
    public static async Task<string> TokenAsync(ApiFixture api)
    {
        await ContaAsync(api.App, Email);
        using var resposta = await SessaoAsync(api.NovoHttp(), Email, Senha);
        var texto = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, "corpo: {0}", texto);
        return JsonDocument.Parse(texto).RootElement.GetProperty("accessToken").GetString()!;
    }

    public static async Task<HttpResponseMessage> SessaoAsync(HttpClient http, string email, string senha)
    {
        using (http)
        {
            return await http.PostAsJsonAsync(new Uri("/api/v1/nebrys/sessao", UriKind.Relative), new { email, senha });
        }
    }

    public static async Task<HttpResponseMessage> EnviarAsync(
        HttpClient http, HttpMethod metodo, string rota, string? token, HttpContent? corpo = null)
    {
        using (http)
        {
            using var requisicao = new HttpRequestMessage(metodo, new Uri(rota, UriKind.Relative)) { Content = corpo };
            if (token is not null)
                requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await http.SendAsync(requisicao);
        }
    }
}
