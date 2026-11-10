using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Api.Composicao;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Jobs;
using Acordu.Api.Operacao;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Acordu.Api.FunctionalTests;

/// <summary>Spec do F13, F13b: avisos de erro em #alertas, com deduplicação e teto (F13-05 a F13-11).</summary>
[Collection(ColecaoApi.Nome)]
public sealed class AvisosDeErroTests(ApiFixture api)
{
    /// <summary>Uma Api com o webhook de #alertas e <paramref name="rotas"/> rotas que lançam, cada uma com impressão própria.</summary>
    private async Task<(WebApplication App, string[] Rotas)> ApiAsync(
        DiscordFalso discord, int rotas = 1, string? teto = null, bool comWebhook = true, LogsCapturados? logs = null)
    {
        var sufixo = Guid.NewGuid().ToString("N");
        var caminhos = Enumerable.Range(0, rotas).Select(i => $"/teste/aviso-{sufixo}/{i}").ToArray();
        var configuracao = new Dictionary<string, string?>
        {
            ["Discord:Alertas"] = comWebhook ? DiscordFalso.Alertas : null,
            ["App:Versao"] = "abc1234",
            ["Operacao:AvisosPorHora"] = teto,
            ["Operacao:LinkErros"] = "https://console.cloud.google.com/errors",
        };
        if (logs is not null)
            configuracao["Logs:Formato"] = "json";

        var app = await api.NovaApiAsync(
            configuracao,
            mapear: grupos =>
            {
                foreach (var caminho in caminhos)
                    grupos.Base.MapGet(caminho, IResult () => throw new InvalidOperationException("detalhe que não vai ao Discord"));
            },
            servicos: s =>
            {
                s.AddHttpClient(ComposicaoApi.NomeClienteDiscord).ConfigurePrimaryHttpMessageHandler(() => discord);
                if (logs is not null)
                    s.AddSingleton<ILoggerProvider>(logs);
            });
        if (logs is not null)
            logs.Formatador = app.Services.GetServices<ConsoleFormatter>().OfType<FormatadorLogJson>().Single();
        return (app, caminhos.Select(c => "/api/v1" + c).ToArray());
    }

    private static async Task<HttpStatusCode> ChamarAsync(WebApplication app, string rota)
    {
        using var http = app.GetTestClient();
        using var resposta = await http.GetAsync(new Uri(rota, UriKind.Relative));
        return resposta.StatusCode;
    }

    private async Task<int> TotalAsync(string rota)
    {
        await using var db = api.DaEquipe(Guid.NewGuid());
        return await db.Database.SqlQuery<int>($"SELECT total AS \"Value\" FROM ocorrencias_erro WHERE rota = {"GET " + rota}")
            .SingleOrDefaultAsync();
    }

    /// <summary>Roda <paramref name="corpo"/> com o relógio em <paramref name="instante"/> e devolve o relógio depois.</summary>
    private async Task NoInstanteAsync(DateTimeOffset instante, Func<Task> corpo)
    {
        var antes = api.Relogio.Agora;
        api.Relogio.Agora = instante;
        try
        {
            await corpo();
        }
        finally
        {
            api.Relogio.Agora = antes;
        }
    }

    [Fact]
    public async Task Erro500_Novo_AvisaComAImpressaoSemAMensagemEComMencoesDesligadas() // F13-05, F13-11
    {
        var discord = new DiscordFalso();
        var (app, rotas) = await ApiAsync(discord);
        await using var montada = app;

        (await ChamarAsync(app, rotas[0])).Should().Be(HttpStatusCode.InternalServerError);

        var mensagem = discord.Mensagens.Should().ContainSingle().Subject;
        mensagem.Webhook.Should().Be(new Uri(DiscordFalso.Alertas));
        var texto = mensagem.Corpo.GetProperty("content").GetString()!;
        texto.Should().StartWith("**Erro novo** · acordu-api `abc1234`");
        texto.Should().Contain("`System.InvalidOperationException` em `Acordu.Api.FunctionalTests.");
        texto.Should().Contain($"`GET {rotas[0]}`").And.Contain("https://console.cloud.google.com/errors");
        texto.Should().NotContain("detalhe que não vai ao Discord");
        mensagem.Corpo.GetProperty("allowed_mentions").GetProperty("parse").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Erro500_RepetidoEm24h_NaoAvisaEConta() // F13-06
    {
        var discord = new DiscordFalso();
        var (app, rotas) = await ApiAsync(discord);
        await using var montada = app;

        await ChamarAsync(app, rotas[0]);
        await NoInstanteAsync(api.Relogio.Agora.AddHours(23), () => ChamarAsync(app, rotas[0]));

        discord.Textos(DiscordFalso.Alertas).Should().ContainSingle();
        (await TotalAsync(rotas[0])).Should().Be(2);
    }

    [Fact]
    public async Task Erro500_RepetidoDepoisDe24h_AvisaDeNovoComAContagem() // F13-07
    {
        var discord = new DiscordFalso();
        var (app, rotas) = await ApiAsync(discord);
        await using var montada = app;
        var inicio = api.Relogio.Agora;

        await ChamarAsync(app, rotas[0]);
        await NoInstanteAsync(inicio.AddHours(1), () => ChamarAsync(app, rotas[0]));
        await NoInstanteAsync(inicio.AddHours(24), () => ChamarAsync(app, rotas[0]));

        var textos = discord.Textos(DiscordFalso.Alertas);
        textos.Should().HaveCount(2);
        textos[1].Should().StartWith("**Erro de novo** (2 vezes desde o último aviso)");
    }

    [Fact]
    public async Task Erro500_TetoDaHora_SeguraEResumeNoProximoAviso() // F13-08
    {
        var discord = new DiscordFalso();
        var (app, rotas) = await ApiAsync(discord, rotas: 4, teto: "2");
        await using var montada = app;
        // Uma hora só deste teste: os avisos dos outros não contam no teto.
        var hora = new DateTimeOffset(2031, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(Random.Shared.Next(0, 500_000) * 3);

        await NoInstanteAsync(hora, async () =>
        {
            await ChamarAsync(app, rotas[0]);
            await ChamarAsync(app, rotas[1]);
            await ChamarAsync(app, rotas[2]);
        });
        discord.Textos(DiscordFalso.Alertas).Should().HaveCount(2, "o terceiro passou do teto e ficou segurado");

        await NoInstanteAsync(hora.AddMinutes(61), () => ChamarAsync(app, rotas[3]));

        var textos = discord.Textos(DiscordFalso.Alertas);
        textos.Should().HaveCount(3);
        textos[2].Should().StartWith("**Erro novo**").And.Contain("+1 aviso segurado pelo teto da hora.");
    }

    [Fact]
    public async Task Erro500_DiscordLento_RespostaNaoEsperaMaisQueOPrazo() // F13-09
    {
        var discord = new DiscordFalso(async ct =>
        {
            await Task.Delay(TimeSpan.FromSeconds(20), ct);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var logs = new LogsCapturados();
        var (app, rotas) = await ApiAsync(discord, logs: logs);
        await using var montada = app;

        var cronometro = Stopwatch.StartNew();
        var status = await ChamarAsync(app, rotas[0]);

        status.Should().Be(HttpStatusCode.InternalServerError);
        cronometro.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(4.5));
        logs.Linhas.Select(l => l.GetProperty("message").GetString()).Should().Contain(
            m => m!.StartsWith("Aviso de operação (Alertas) não saiu", StringComparison.Ordinal)
                || m.StartsWith("Aviso do erro inesperado passou do prazo", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Erro500_DiscordFora_RespostaIgualEFalhaNoLogSemAUrl() // F13-09
    {
        var discord = new DiscordFalso(_ => throw new HttpRequestException($"falhou ao chamar {DiscordFalso.Alertas}"));
        var logs = new LogsCapturados(LogLevel.Information);
        var (app, rotas) = await ApiAsync(discord, logs: logs);
        await using var montada = app;

        (await ChamarAsync(app, rotas[0])).Should().Be(HttpStatusCode.InternalServerError);

        var avisos = logs.Linhas.Where(l => l.GetProperty("severity").GetString() == "WARNING").Select(l => l.GetRawText()).ToList();
        avisos.Should().Contain(l => l.Contains("Aviso de operação (Alertas) não saiu", StringComparison.Ordinal));
        logs.Linhas.Select(l => l.GetRawText()).Should().NotContain(
            l => l.Contains("alertas-dos-testes", StringComparison.Ordinal),
            "o webhook é segredo: nem o log do HttpClient (Information) pode trazer a URL");
    }

    [Fact]
    public async Task Erro500_AvisoNaoSaiu_OMesmoErroAvisaNaProximaVez() // F13-09
    {
        var tentativas = 0;
        var discord = new DiscordFalso(_ => Task.FromResult(new HttpResponseMessage(
            Interlocked.Increment(ref tentativas) == 1 ? HttpStatusCode.InternalServerError : HttpStatusCode.NoContent)));
        var (app, rotas) = await ApiAsync(discord);
        await using var montada = app;

        await ChamarAsync(app, rotas[0]);
        await ChamarAsync(app, rotas[0]);

        var textos = discord.Textos(DiscordFalso.Alertas);
        textos.Should().HaveCount(2, "o primeiro não saiu e a reserva foi desfeita");
        textos[1].Should().StartWith("**Erro novo**");
        (await TotalAsync(rotas[0])).Should().Be(2);
    }

    [Fact]
    public async Task Erro500_SemWebhook_NadaSaiENadaGrava() // F13-10
    {
        var discord = new DiscordFalso();
        var (app, rotas) = await ApiAsync(discord, comWebhook: false);
        await using var montada = app;

        (await ChamarAsync(app, rotas[0])).Should().Be(HttpStatusCode.InternalServerError);

        discord.Mensagens.Should().BeEmpty();
        (await TotalAsync(rotas[0])).Should().Be(0);
    }

    [Fact]
    public async Task ErroDeRegra_4xx_NaoAvisa()
    {
        var discord = new DiscordFalso();
        var rota = $"/teste/regra-{Guid.NewGuid():N}";
        await using var app = await api.NovaApiAsync(
            new Dictionary<string, string?> { ["Discord:Alertas"] = DiscordFalso.Alertas },
            mapear: grupos => grupos.Base.MapGet(rota, IResult () => throw new ArgumentException("Nome inválido.", "nome")),
            servicos: s => s.AddHttpClient(ComposicaoApi.NomeClienteDiscord).ConfigurePrimaryHttpMessageHandler(() => discord));

        (await ChamarAsync(app, "/api/v1" + rota)).Should().Be(HttpStatusCode.BadRequest);

        discord.Mensagens.Should().BeEmpty();
    }

    [Fact]
    public async Task WebhookSemHttps_NaoSobe()
    {
        var subir = () => api.NovaApiAsync(new Dictionary<string, string?> { ["Discord:Alertas"] = "http://discord.com/api/webhooks/x" });

        (await subir.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*Discord__Alertas*");
    }

    [Fact]
    public async Task Diario_OcorrenciaSemRepeticaoHa90Dias_Apaga()
    {
        var discord = new DiscordFalso();
        var (app, rotas) = await ApiAsync(discord);
        await using var montada = app;
        await ChamarAsync(app, rotas[0]);
        JsonElement resultado = default;

        await NoInstanteAsync(api.Relogio.Agora.AddDays(91), async () =>
        {
            using var http = api.NovoHttp();
            using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/internal/jobs/diario", UriKind.Relative));
            requisicao.Headers.Add(JobsEndpoints.CabecalhoToken, ApiFixture.TokenJobs);
            using var resposta = await http.SendAsync(requisicao);
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            resultado = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        });

        resultado.GetProperty("ocorrenciasErroRemovidas").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        resultado.GetProperty("falhas").GetInt32().Should().Be(0);
        (await TotalAsync(rotas[0])).Should().Be(0);
    }
}
