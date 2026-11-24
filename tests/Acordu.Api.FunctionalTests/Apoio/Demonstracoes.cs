using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Infrastructure.Persistence;
using Acordu.Infrastructure.Persistence.Cadastros;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Api.FunctionalTests.Apoio;

/// <summary>
/// A empresa de demonstração nos testes (F14s): há uma só por banco, e o banco é o mesmo para toda a coleção. Quem testa a
/// criação apaga a que houver antes (pelo mesmo <see cref="ApagadorDeEmpresa"/> do expurgo, que leva as contas fictícias);
/// os demais reaproveitam a que existe.
/// </summary>
internal static class Demonstracoes
{
    public const string Rota = "/api/v1/nebrys/demonstracao";

    /// <summary>Apaga a demonstração que houver, com os dados e as contas que só tinham presença nela.</summary>
    public static async Task ApagarAsync(ApiFixture api)
    {
        await using var escopo = api.App.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AcorduDbContext>();
        var apagador = escopo.ServiceProvider.GetRequiredService<ApagadorDeEmpresa>();
        var existentes = await db.Tenants.IgnoreQueryFilters().AsNoTracking().Where(t => t.Demonstracao).ToListAsync();
        foreach (var demonstracao in existentes)
        {
            await using var transacao = await db.Database.BeginTransactionAsync();
            await db.Tenants.IgnoreQueryFilters().Where(t => t.Id == demonstracao.Id).ExecuteDeleteAsync();
            await apagador.ApagarDadosAsync(db, demonstracao, CancellationToken.None);
            await transacao.CommitAsync();
        }
    }

    /// <summary>Cria a demonstração pela área (apagando a que houver) e devolve o corpo da resposta.</summary>
    public static async Task<JsonElement> CriarAsync(ApiFixture api, string token)
    {
        await ApagarAsync(api);
        using var resposta = await EnviarAsync(api, HttpMethod.Post, Rota, token);
        var texto = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.Created, "corpo: {0}", texto);
        return JsonDocument.Parse(texto).RootElement.Clone();
    }

    /// <summary>A demonstração que existe, ou uma nova.</summary>
    public static async Task<Guid> GarantirAsync(ApiFixture api, string token)
    {
        using var resposta = await EnviarAsync(api, HttpMethod.Get, Rota, token);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        if (corpo.GetProperty("empresaId").ValueKind == JsonValueKind.String)
            return corpo.GetProperty("empresaId").GetGuid();
        return (await CriarAsync(api, token)).GetProperty("empresaId").GetGuid();
    }

    /// <summary>Indica o e-mail para a demonstração, conferindo o <c>200</c>.</summary>
    public static async Task IndicarAsync(ApiFixture api, string token, string email)
    {
        using var resposta = await EnviarAsync(api, HttpMethod.Post, $"{Rota}/indicacoes", token, new { email });
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, "corpo: {0}", await resposta.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// A sessão da pessoa (o Dono da <paramref name="equipe"/>) trocada para a demonstração: o access token dela lá. A
    /// troca revoga o refresh da sessão de origem, como no app.
    /// </summary>
    public static async Task<string> SessaoNaDemonstracaoAsync(ApiFixture api, Equipe equipe, Guid demonstracao)
    {
        using var http = api.NovoHttp();
        using var troca = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v1/auth/tenant-ativo", UriKind.Relative))
        {
            Content = JsonContent.Create(new { tenantId = demonstracao }),
        };
        troca.Headers.Authorization = new AuthenticationHeaderValue("Bearer", equipe.Dono);
        troca.Headers.Add("Cookie", $"acordu_refresh={equipe.RefreshDono}");
        using var resposta = await http.SendAsync(troca);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, "a pessoa indicada entra na demonstração");
        return (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }

    public static Task<HttpResponseMessage> EnviarAsync(
        ApiFixture api, HttpMethod metodo, string rota, string token, object? corpo = null) =>
        OperadorNebrys.EnviarAsync(api.NovoHttp(), metodo, rota, token, corpo is null ? null : JsonContent.Create(corpo));
}
