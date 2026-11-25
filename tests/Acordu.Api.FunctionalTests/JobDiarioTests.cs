using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Api.Composicao;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Jobs;
using Acordu.Domain.Entities;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F8, cenários F8-06 e F8-07: o disparo diário do Scheduler (ADR 0010 §5); e o expurgo dos convites de empresa
/// (#213).
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class JobDiarioTests(ApiFixture api)
{
    [Fact]
    public async Task Diario_ComToken_FechaExpurgaESomaAsContagens()
    {
        var equipe = await Equipe.CriarAsync(api);
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, "/api/v1/configuracoes/portal", new { diasFechamentoAutomatico = 2 });
        var solicitacao = await Cenarios.SolicitacaoAsync(equipe, await Cenarios.ClienteAsync(equipe));
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/resolucao",
            new { solucao = new { texto = "Feito" } });
        // Em 91 dias a empresa em TESTE venceria no próprio diário e ficaria só leitura, fora do fechamento (F14): ativa,
        // gravada direto (as ações da área da Nebrys são do F14l).
        await using (var db = api.DaEquipe(equipe.TenantId))
            await db.Database.ExecuteSqlAsync($"UPDATE tenants SET situacao = 'ATIVA', fim_do_teste = NULL WHERE id = {equipe.TenantId}");

        // 91 dias depois: a solicitação passou do fechamento (2 dias) e a atividade da abertura passou da retenção do
        // feed (90 dias), então o fechamento e o expurgo precisam ter rodado.
        var inicio = api.Relogio.Agora;
        api.Relogio.Agora = inicio.AddDays(91);
        JsonElement resultado;
        try
        {
            using var http = api.NovoHttp();
            using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/internal/jobs/diario", UriKind.Relative));
            requisicao.Headers.Add(JobsEndpoints.CabecalhoToken, ApiFixture.TokenJobs);
            using var resposta = await http.SendAsync(requisicao);
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            resultado = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        }
        finally
        {
            api.Relogio.Agora = inicio;
        }

        resultado.GetProperty("fechadas").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        resultado.GetProperty("falhas").GetInt32().Should().Be(0);
        resultado.GetProperty("atividadesRemovidas").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        resultado.GetProperty("auditoriaRemovida").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        resultado.GetProperty("notificacoesRemovidas").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}"))
            .GetProperty("status").GetString().Should().Be("FECHADA");
    }

    [Fact] // #213
    public async Task Diario_ConviteDeEmpresaRevogadoHa93Dias_ApagaEContaNoResumoEOVencidoHaMenosFica()
    {
        var token = await OperadorNebrys.TokenAsync(api);
        var revogado = await EmitidoAsync(token);
        using (var resposta = await OperadorNebrys.EnviarAsync(
            api.NovoHttp(), HttpMethod.Post, $"/api/v1/nebrys/convites/{revogado}/revogacao", token))
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        // Pendente com a validade padrão (7 dias): em 93 dias está vencido há 86, menos que a retenção.
        var vencido = await EmitidoAsync(token);
        var discord = new DiscordFalso();
        await using var app = await api.NovaApiAsync(
            new Dictionary<string, string?> { ["Discord:Deploy"] = DiscordFalso.Deploy, ["Discord:Alertas"] = DiscordFalso.Alertas },
            servicos: s => s.AddHttpClient(ComposicaoApi.NomeClienteDiscord).ConfigurePrimaryHttpMessageHandler(() => discord));

        // 93 dias: um dia só deste teste, porque a marca do resumo do dia fica no banco compartilhado.
        var inicio = api.Relogio.Agora;
        api.Relogio.Agora = inicio.AddDays(93);
        JsonElement resultado;
        try
        {
            using var http = app.GetTestClient();
            resultado = await DiarioAsync(http);
        }
        finally
        {
            api.Relogio.Agora = inicio;
        }

        resultado.GetProperty("convitesRemovidos").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        resultado.GetProperty("falhas").GetInt32().Should().Be(0);
        discord.Textos(DiscordFalso.Deploy).Should().ContainSingle().Which.Should().MatchRegex("convites [1-9][0-9]*,");
        await using var db = api.DaEquipe(Guid.NewGuid());
        (await db.Set<ConviteEmpresa>().AnyAsync(c => c.Id == revogado)).Should().BeFalse("revogado há 93 dias");
        (await db.Set<ConviteEmpresa>().AnyAsync(c => c.Id == vencido)).Should().BeTrue("vencido há 86 dias");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("token-errado")]
    public async Task Diario_SemTokenOuComTokenErrado_404(string? token)
    {
        using var http = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/internal/jobs/diario", UriKind.Relative));
        if (token is not null)
            requisicao.Headers.Add(JobsEndpoints.CabecalhoToken, token);

        using var resposta = await http.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<Guid> EmitidoAsync(string token)
    {
        using var corpo = JsonContent.Create(new { email = $"convite-diario-{Guid.NewGuid():N}@exemplo.com" });
        using var resposta = await OperadorNebrys.EnviarAsync(api.NovoHttp(), HttpMethod.Post, "/api/v1/nebrys/convites", token, corpo);
        var texto = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.Created, "corpo: {0}", texto);
        using var documento = JsonDocument.Parse(texto);
        return documento.RootElement.GetProperty("convite").GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> DiarioAsync(HttpClient http)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/internal/jobs/diario", UriKind.Relative));
        requisicao.Headers.Add(JobsEndpoints.CabecalhoToken, ApiFixture.TokenJobs);
        using var resposta = await http.SendAsync(requisicao);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await resposta.Content.ReadFromJsonAsync<JsonElement>();
    }
}
