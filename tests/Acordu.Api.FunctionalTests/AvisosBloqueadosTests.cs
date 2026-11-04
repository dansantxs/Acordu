using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Jobs;
using Acordu.Domain.Entities;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// RF-AUT-06 (spec do F9, F9b): em empresa não liberada, os avisos automáticos por e-mail a terceiros não saem (nem o
/// link de acesso); o criador continua recebendo os dele, e o sino não muda. Bloqueado é descartado, não adiado.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class AvisosBloqueadosTests(ApiFixture api)
{
    [Fact]
    public async Task AbrirSolicitacao_EmpresaNaoLiberada_CriaSemAvisarNemGerarLink() // F9-15
    {
        var equipe = await Equipe.CriarAsync(api, confirmar: false);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var (contato, email) = await ContatoAsync(equipe, cliente);

        var solicitacao = await AbrirAsync(equipe, cliente, contato);

        api.Emails.Para(email).Should().BeEmpty();
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}/acessos-portal"))
            .GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task AvisosAoContato_EmpresaNaoLiberada_OperacoesSeguemSemEmail() // F9-16
    {
        var equipe = await Equipe.CriarAsync(api, confirmar: false);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var (contato, email) = await ContatoAsync(equipe, cliente);
        var solicitacao = await AbrirAsync(equipe, cliente, contato);

        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/interacoes",
            new { texto = "Pode mandar a logo em alta resolução?", visibilidade = "EXTERNA" });
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/aguardando-cliente");
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/resolucao",
            new { solucao = new { texto = "Logo trocada" } });

        api.Emails.Para(email).Should().BeEmpty();
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}/acessos-portal"))
            .GetArrayLength().Should().Be(0, "nenhum aviso gerou link");
    }

    [Fact]
    public async Task AvisoInterno_EmpresaNaoLiberada_EmailSoAoCriadorESinoATodos() // F9-17
    {
        var equipe = await Equipe.CriarAsync(api, confirmar: false);
        var emailCriador = await EmailAsync(equipe, equipe.Dono);
        var outroDono = await equipe.ComPapelAsync(Papel.NomeDono);
        var emailOutro = await EmailAsync(equipe, outroDono);

        await CruzarConsumoAsync(equipe);

        api.Emails.Para(emailCriador).Should().Contain(m => m.Categoria == "aviso-consumo");
        api.Emails.Para(emailOutro).Should().NotContain(m => m.Categoria == "aviso-consumo");
        foreach (var token in new[] { equipe.Dono, outroDono })
        {
            var sino = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/notificacoes", token: token);
            sino.GetProperty("itens").EnumerateArray()
                .Should().Contain(n => n.GetProperty("categoria").GetString() == "CONSUMO", "o sino não depende da liberação");
        }
    }

    [Fact]
    public async Task ConfirmarEmail_DepoisDeAvisosBloqueados_NaoEntregaOsAntigos() // F9-21
    {
        var equipe = await Equipe.CriarAsync(api, confirmar: false);
        var emailCriador = await EmailAsync(equipe, equipe.Dono);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var (contato, email) = await ContatoAsync(equipe, cliente);
        var solicitacao = await AbrirAsync(equipe, cliente, contato);

        await PostAnonimoAsync("/api/v1/auth/confirmacao-email", new { token = api.Emails.TokenDe(emailCriador, "confirmacao-email") });
        await TickAsync();

        api.Emails.Para(email).Should().BeEmpty("o aviso de recebida foi descartado, não adiado");
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/aguardando-cliente");
        api.Emails.Para(email).Should().ContainSingle().Which.Categoria.Should().Be("aviso-aguardando");
    }

    private static async Task<string> EmailAsync(Equipe equipe, string token) =>
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: token)).GetProperty("email").GetString()!;

    private static async Task<Guid> AbrirAsync(Equipe equipe, Guid cliente, Guid contato) =>
        Cenarios.Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/solicitacoes", new
        {
            clienteId = cliente, contatoId = contato, titulo = "Trocar a logo do site", prioridade = "MEDIA",
        }));

    private static async Task<(Guid Id, string Email)> ContatoAsync(Equipe equipe, Guid cliente)
    {
        var email = $"contato-{Guid.NewGuid():N}@bomgrao.com.br";
        var contato = Cenarios.Id(await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/clientes/{cliente}/contatos", new { nome = "Carlos Prado", email }));
        return (contato, email);
    }

    /// <summary>Contrato de 10h e um apontamento de 9h: cruza os 80% e avisa quem tem <c>contrato.gerenciar</c>.</summary>
    private async Task CruzarConsumoAsync(Equipe equipe)
    {
        var cliente = await Cenarios.ClienteAsync(equipe);
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/contratos", new
        {
            clienteId = cliente, tipo = "MENSAL_FIXO", inicio = "2026-09-01", franquiaMinutos = 600, sla = Cenarios.Sla(),
        });
        var tarefa = await Cenarios.TarefaAsync(equipe, await Cenarios.SolicitacaoAsync(equipe, cliente));
        var hoje = DateOnly.FromDateTime(api.Relogio.Agora.UtcDateTime).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/apontamentos", new
        {
            tarefaId = tarefa, data = hoje, duracaoMinutos = 540, descricao = "Trabalho", faturavel = true,
        });
    }

    private async Task PostAnonimoAsync(string rota, object corpo)
    {
        using var http = api.NovoHttp();
        using var resposta = await http.PostAsJsonAsync(new Uri(rota, UriKind.Relative), corpo);
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task TickAsync()
    {
        using var http = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/internal/jobs/tick", UriKind.Relative));
        requisicao.Headers.Add(JobsEndpoints.CabecalhoToken, ApiFixture.TokenJobs);
        using var resposta = await http.SendAsync(requisicao);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        _ = await resposta.Content.ReadFromJsonAsync<JsonElement>();
    }
}
