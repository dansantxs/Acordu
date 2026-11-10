using System.Net;
using System.Text.Json;
using Acordu.Api.Composicao;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Jobs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Acordu.Api.FunctionalTests.Apoio.Cenarios;

namespace Acordu.Api.FunctionalTests;

/// <summary>Spec do F13, F13e: "Sugerir melhoria" na API (F13-23 a F13-30).</summary>
[Collection(ColecaoApi.Nome)]
public sealed class SugestoesTests(ApiFixture api)
{
    private const string Rota = "/api/v1/sugestoes";

    private async Task<(WebApplication App, DiscordFalso Discord)> ApiAsync(DiscordFalso? discord = null)
    {
        discord ??= new DiscordFalso();
        var app = await api.NovaApiAsync(
            new Dictionary<string, string?> { ["Discord:Sugestoes"] = DiscordFalso.Sugestoes },
            servicos: s => s.AddHttpClient(ComposicaoApi.NomeClienteDiscord).ConfigurePrimaryHttpMessageHandler(() => discord));
        return (app, discord);
    }

    private static async Task<(HttpStatusCode Status, JsonElement Corpo)> EnviarAsync(WebApplication app, string? token, object corpo)
    {
        using var http = app.GetTestClient();
        using var pedido = new HttpRequestMessage(HttpMethod.Post, new Uri(Rota, UriKind.Relative))
        {
            Content = System.Net.Http.Json.JsonContent.Create(corpo),
        };
        if (token is not null)
            pedido.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var resposta = await http.SendAsync(pedido);
        var texto = await resposta.Content.ReadAsStringAsync();
        return (resposta.StatusCode, texto.Length == 0 ? default : JsonDocument.Parse(texto).RootElement.Clone());
    }

    private async Task<(Guid? Empresa, Guid? Pessoa, DateTimeOffset? AvisadaEm)> GravadaAsync(Guid id)
    {
        await using var db = api.DaEquipe(Guid.NewGuid());
        var linha = await db.Database
            .SqlQuery<Linha>($"SELECT empresa_id AS \"Empresa\", pessoa_id AS \"Pessoa\", avisada_em AS \"AvisadaEm\" FROM sugestoes WHERE id = {id}")
            .SingleAsync();
        return (linha.Empresa, linha.Pessoa, linha.AvisadaEm);
    }

    private sealed class Linha
    {
        public Guid? Empresa { get; set; }

        public Guid? Pessoa { get; set; }

        public DateTimeOffset? AvisadaEm { get; set; }
    }

    [Fact]
    public async Task Enviar_PelaEquipe_201GravaEAvisaSemAPessoa() // F13-23, F13-28
    {
        var equipe = await Equipe.CriarAsync(api);
        var (app, discord) = await ApiAsync();
        await using var montada = app;

        var (status, corpo) = await EnviarAsync(app, equipe.Dono, new
        {
            texto = "Seria bom filtrar tarefas por cliente. @everyone",
            tela = "/tarefas/7c1f0e2a-1b2c-4d5e-8f90-a1b2c3d4e5f6?aba=2",
        });

        status.Should().Be(HttpStatusCode.Created);
        var numero = corpo.GetProperty("numero").GetInt32();
        numero.Should().BePositive();
        var gravada = await GravadaAsync(corpo.GetProperty("id").GetGuid());
        gravada.Empresa.Should().Be(equipe.TenantId);
        gravada.Pessoa.Should().NotBeNull();
        gravada.AvisadaEm.Should().NotBeNull();
        var mensagem = discord.Mensagens.Should().ContainSingle().Subject;
        mensagem.Webhook.Should().Be(new Uri(DiscordFalso.Sugestoes));
        var texto = mensagem.Corpo.GetProperty("content").GetString()!;
        texto.Should().StartWith($"**Sugestão #{numero}** · Estúdio Traço · tela `/tarefas/:id`\n");
        texto.Should().Contain("Seria bom filtrar tarefas por cliente.");
        texto.Should().NotContain("Pessoa da equipe").And.NotContain("@exemplo.com");
        mensagem.Corpo.GetProperty("allowed_mentions").GetProperty("parse").GetArrayLength().Should().Be(0, "o @everyone do texto não aciona ninguém");
    }

    [Theory]
    [InlineData("curto")]
    [InlineData(null)]
    public async Task Enviar_TextoCurto_400NoCampo(string? texto) // F13-24
    {
        var equipe = await Equipe.CriarAsync(api);

        var (status, corpo) = await EnviarAsync(api.App, equipe.Dono, new { texto });

        status.Should().Be(HttpStatusCode.BadRequest);
        corpo.GetProperty("errors").GetProperty("texto")[0].GetString().Should().Be("Escreva pelo menos 10 caracteres.");
    }

    [Fact]
    public async Task Enviar_TextoLongo_400NoCampo() // F13-24
    {
        var equipe = await Equipe.CriarAsync(api);

        var (status, corpo) = await EnviarAsync(api.App, equipe.Dono, new { texto = new string('a', 2001) });

        status.Should().Be(HttpStatusCode.BadRequest);
        corpo.GetProperty("errors").TryGetProperty("texto", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Enviar_PeloPortal_403() // F13-25
    {
        var equipe = await Equipe.CriarAsync(api);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, await ClienteAsync(equipe));

        var (status, _) = await EnviarAsync(api.App, contato.Token, new { texto = "Uma sugestão do contato." });

        status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Enviar_SemLogin_401() // F13-26
    {
        var (status, _) = await EnviarAsync(api.App, null, new { texto = "Uma sugestão sem login." });

        status.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Enviar_SextaNaHora_429ComAMensagem() // F13-30
    {
        var equipe = await Equipe.CriarAsync(api);

        for (var i = 0; i < 5; i++)
            (await EnviarAsync(api.App, equipe.Dono, new { texto = $"Sugestão número {i} da hora." })).Status.Should().Be(HttpStatusCode.Created);
        var (status, corpo) = await EnviarAsync(api.App, equipe.Dono, new { texto = "A sexta sugestão da hora." });

        status.Should().Be(HttpStatusCode.TooManyRequests);
        corpo.GetProperty("detail").GetString().Should().Be("Muitas sugestões em pouco tempo. Tente de novo mais tarde.");
    }

    [Fact]
    public async Task Enviar_DiscordFora_201EOTickReenvia() // F13-29
    {
        var equipe = await Equipe.CriarAsync(api);
        var fora = true;
        var discord = new DiscordFalso(_ => Task.FromResult(new HttpResponseMessage(fora ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.NoContent)));
        var (app, _) = await ApiAsync(discord);
        await using var montada = app;

        var (status, corpo) = await EnviarAsync(app, equipe.Dono, new { texto = "Sugestão que espera o Discord voltar." });
        status.Should().Be(HttpStatusCode.Created);
        var id = corpo.GetProperty("id").GetGuid();
        (await GravadaAsync(id)).AvisadaEm.Should().BeNull("o Discord estava fora");

        fora = false;
        var antes = api.Relogio.Agora;
        api.Relogio.Agora = antes.AddMinutes(2);
        JsonElement tick;
        try
        {
            using var http = app.GetTestClient();
            using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/internal/jobs/tick", UriKind.Relative));
            requisicao.Headers.Add(JobsEndpoints.CabecalhoToken, ApiFixture.TokenJobs);
            using var resposta = await http.SendAsync(requisicao);
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            tick = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement.Clone();
        }
        finally
        {
            api.Relogio.Agora = antes;
        }

        tick.GetProperty("sugestoesReenviadas").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        (await GravadaAsync(id)).AvisadaEm.Should().NotBeNull();
        discord.Textos(DiscordFalso.Sugestoes).Should().HaveCountGreaterThanOrEqualTo(2)
            .And.Contain(t => t.Contains("Sugestão que espera o Discord voltar.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Enviar_SemWebhook_201EFicaPendenteSemFalhar()
    {
        var equipe = await Equipe.CriarAsync(api);

        var (status, corpo) = await EnviarAsync(api.App, equipe.Dono, new { texto = "Sugestão sem webhook configurado." });

        status.Should().Be(HttpStatusCode.Created);
        (await GravadaAsync(corpo.GetProperty("id").GetGuid())).AvisadaEm.Should().BeNull();
    }
}
