using System.Net;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Jobs;
using Acordu.Domain.Entities;

namespace Acordu.Api.FunctionalTests;

/// <summary>Spec do F12, F12e: cronômetro esquecido no app e no tick (F12-17 a F12-19).</summary>
[Collection(ColecaoApi.Nome)]
public sealed class CronometroEsquecidoTests(ApiFixture api)
{
    [Fact]
    public async Task Rodando10h_GetDizEsquecido_ETickAvisaODonoUmaVez() // F12-17, F12-18, F12-19
    {
        var equipe = await Equipe.CriarAsync(api);
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);
        var (_, emailMembro) = await PessoaAsync(equipe, membro);
        var (_, emailDono) = await PessoaAsync(equipe, equipe.Dono);
        var tarefa = await Cenarios.TarefaAsync(equipe, titulo: "Relatório mensal");
        // 10 h e meia atrás: passa do limite de 10 h, qualquer que seja o expediente.
        var criado = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/cronometro/inicio", new
        {
            tarefaId = tarefa, iniciadoEm = api.Relogio.Agora.AddMinutes(-630),
        }, membro);
        criado.GetProperty("esquecido").GetBoolean().Should().BeTrue("o 201 traz a mesma leitura do GET");

        var ativo = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/cronometro", token: membro);
        ativo.GetProperty("esquecido").GetBoolean().Should().BeTrue();
        // A sugestão só vem quando o fim do expediente já passou; aqui, pode vir nula ou um número que nunca passa do decorrido.
        if (ativo.GetProperty("sugestaoMinutos").ValueKind == JsonValueKind.Number)
            ativo.GetProperty("sugestaoMinutos").GetInt32().Should().BeLessThanOrEqualTo(ativo.GetProperty("decorridoMinutos").GetInt32());

        // Outra empresa com cronômetro esquecido: o aviso de cada uma vai só ao dono do cronômetro, no tenant dele.
        var outra = await Equipe.CriarAsync(api);
        var (_, emailOutra) = await PessoaAsync(outra, outra.Dono);
        await outra.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/cronometro/inicio", new
        {
            tarefaId = await Cenarios.TarefaAsync(outra), iniciadoEm = api.Relogio.Agora.AddMinutes(-630),
        });

        await TickAsync();
        await TickAsync();

        var aviso = api.Emails.Para(emailMembro).Should().ContainSingle(m => m.Categoria == "aviso-cronometro-esquecido").Subject;
        aviso.Texto.Should().Contain("Relatório mensal").And.Contain($"/tarefas/{tarefa}");
        api.Emails.Para(emailDono).Should().NotContain(m => m.Categoria == "aviso-cronometro-esquecido");
        api.Emails.Para(emailOutra).Should().ContainSingle(m => m.Categoria == "aviso-cronometro-esquecido");

        (await AvisosDeCronometroAsync(equipe, membro)).Should().Be(1);
        (await AvisosDeCronometroAsync(equipe, equipe.Dono)).Should().Be(0);
        (await AvisosDeCronometroAsync(outra, outra.Dono)).Should().Be(1);
    }

    private static async Task<int> AvisosDeCronometroAsync(Equipe equipe, string token) =>
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/notificacoes", token: token))
            .GetProperty("itens").EnumerateArray().Count(n => n.GetProperty("categoria").GetString() == "CRONOMETRO");

    [Fact]
    public async Task RecemIniciado_NaoEstaEsquecido_ETickNaoAvisa()
    {
        var equipe = await Equipe.CriarAsync(api);
        var (_, emailDono) = await PessoaAsync(equipe, equipe.Dono);
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/cronometro/inicio", new
        {
            tarefaId = await Cenarios.TarefaAsync(equipe),
        });

        var ativo = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/cronometro");
        await TickAsync();

        ativo.GetProperty("esquecido").GetBoolean().Should().BeFalse();
        api.Emails.Para(emailDono).Should().NotContain(m => m.Categoria == "aviso-cronometro-esquecido");
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, "/api/v1/cronometro");
    }

    [Fact]
    public async Task EmailDesligadoNaCategoria_SoOSino()
    {
        var equipe = await Equipe.CriarAsync(api);
        var (_, emailDono) = await PessoaAsync(equipe, equipe.Dono);
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, "/api/v1/notificacoes/preferencias", new
        {
            categorias = new[] { new { categoria = "CRONOMETRO", email = false } },
        });
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/cronometro/inicio", new
        {
            tarefaId = await Cenarios.TarefaAsync(equipe), iniciadoEm = api.Relogio.Agora.AddMinutes(-630),
        });

        await TickAsync();

        api.Emails.Para(emailDono).Should().NotContain(m => m.Categoria == "aviso-cronometro-esquecido");
        var sino = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/notificacoes");
        sino.GetProperty("itens").EnumerateArray().Should().ContainSingle(n => n.GetProperty("categoria").GetString() == "CRONOMETRO");
    }

    private static async Task<(Guid Id, string Email)> PessoaAsync(Equipe equipe, string token)
    {
        var eu = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: token);
        return (eu.GetProperty("usuarioId").GetGuid(), eu.GetProperty("email").GetString()!);
    }

    private async Task TickAsync()
    {
        using var http = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/internal/jobs/tick", UriKind.Relative));
        requisicao.Headers.Add(JobsEndpoints.CabecalhoToken, ApiFixture.TokenJobs);
        using var resposta = await http.SendAsync(requisicao);
        var corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonDocument.Parse(corpo).RootElement.GetProperty("falhas").GetInt32().Should().Be(0, "tick: {0}", corpo);
    }
}
