using System.Net;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Jobs;
using Acordu.Domain.Entities;

namespace Acordu.Api.FunctionalTests;

/// <summary>RF-NOT-02: nova solicitação, atribuição e alertas de SLA para a equipe.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class AvisosAEquipeTests(ApiFixture api)
{
    [Fact]
    public async Task NovaSolicitacao_VaiParaQuemEdita_MenosQuemAbriu_EPeloPortalParaTodos()
    {
        var equipe = await Equipe.CriarAsync(api);
        var (_, emailDono) = await PessoaAsync(equipe, equipe.Dono);
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);
        var (_, emailMembro) = await PessoaAsync(equipe, membro);
        var leitor = await equipe.ComPapelAsync(Papel.NomeLeitor);
        var (_, emailLeitor) = await PessoaAsync(equipe, leitor);
        var cliente = await Cenarios.ClienteAsync(equipe);

        await Cenarios.SolicitacaoAsync(equipe, cliente);

        api.Emails.Para(emailDono).Should().NotContain(m => m.Categoria == "aviso-nova-solicitacao", "quem abriu não é avisado");
        api.Emails.Para(emailMembro).Should().ContainSingle(m => m.Categoria == "aviso-nova-solicitacao");
        api.Emails.Para(emailLeitor).Should().NotContain(m => m.Categoria == "aviso-nova-solicitacao", "Leitor não edita solicitações");

        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/portal/solicitacoes", new { titulo = "Pelo portal" }, contato.Token);

        var aviso = api.Emails.Para(emailDono).Last(m => m.Categoria == "aviso-nova-solicitacao");
        aviso.Assunto.Should().Contain("Pelo portal");
        aviso.Texto.Should().Contain("pelo cliente, no portal").And.Contain("/solicitacoes/");
    }

    [Fact]
    public async Task ComResponsavel_SoEleRecebe_EAtribuicaoNaoAvisaQuemSeAtribuiu()
    {
        var equipe = await Equipe.CriarAsync(api);
        var (idDono, emailDono) = await PessoaAsync(equipe, equipe.Dono);
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);
        var (idMembro, emailMembro) = await PessoaAsync(equipe, membro);
        var cliente = await Cenarios.ClienteAsync(equipe);

        var solicitacao = Cenarios.Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/solicitacoes", new
        {
            clienteId = cliente, titulo = "Já com responsável", prioridade = "ALTA", responsavelId = idMembro,
        }, membro));
        api.Emails.Para(emailMembro).Should().NotContain(m => m.Categoria == "aviso-nova-solicitacao");
        api.Emails.Para(emailDono).Should().NotContain(m => m.Categoria == "aviso-nova-solicitacao", "com responsável, só ele");

        await Editar(equipe, solicitacao, idDono, equipe.Dono);
        api.Emails.Para(emailDono).Should().NotContain(m => m.Categoria == "aviso-atribuicao", "atribuiu a si mesmo");

        await Editar(equipe, solicitacao, idMembro, equipe.Dono);
        var atribuicao = api.Emails.Para(emailMembro).Single(m => m.Categoria == "aviso-atribuicao");
        atribuicao.Assunto.Should().Contain("atribuída a você");
    }

    [Fact]
    public async Task Sla_AlertaDeRiscoEDeViolacao_UmaVezCada()
    {
        // O relógio do fixture é compartilhado e só anda para frente: começa às 10h (Brasília) do próximo dia útil,
        // para os 60 minutos úteis do SLA caberem no expediente.
        var original = api.Relogio.Agora;
        var dia = original.UtcDateTime.Date.AddDays(1);
        while (dia.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            dia = dia.AddDays(1);
        var inicio = new DateTimeOffset(dia.AddHours(13), TimeSpan.Zero);
        api.Relogio.Agora = inicio;

        var equipe = await Equipe.CriarAsync(api);
        var (idDono, emailDono) = await PessoaAsync(equipe, equipe.Dono);
        var cliente = await Cenarios.ClienteAsync(equipe);
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/contratos", new
        {
            clienteId = cliente, tipo = "BANCO_HORAS", inicio = "2026-09-01", franquiaMinutos = 600, sla = Cenarios.Sla(60, 60),
        });
        var solicitacao = Cenarios.Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/solicitacoes", new
        {
            clienteId = cliente, titulo = "Servidor caiu", prioridade = "URGENTE", responsavelId = idDono,
        }));

        try
        {
            await TickAsync();
            api.Emails.Para(emailDono).Should().NotContain(m => m.Categoria.StartsWith("aviso-sla", StringComparison.Ordinal));

            api.Relogio.Agora = inicio.AddMinutes(50);
            await TickAsync();
            await TickAsync();
            api.Emails.Para(emailDono).Should().ContainSingle(m => m.Categoria == "aviso-sla-risco");

            api.Relogio.Agora = inicio.AddMinutes(70);
            await TickAsync();
            await TickAsync();
            var violado = api.Emails.Para(emailDono).Single(m => m.Categoria == "aviso-sla-violado");
            violado.Assunto.Should().Contain("SLA violado");
            violado.Texto.Should().Contain($"/solicitacoes/{solicitacao}");
            api.Emails.Para(emailDono).Should().ContainSingle(m => m.Categoria == "aviso-sla-risco");
        }
        finally
        {
            api.Relogio.Agora = inicio.AddMinutes(70);
        }
    }

    private static async Task<(Guid Id, string Email)> PessoaAsync(Equipe equipe, string token)
    {
        var eu = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: token);
        return (eu.GetProperty("usuarioId").GetGuid(), eu.GetProperty("email").GetString()!);
    }

    private static Task<JsonElement> Editar(Equipe equipe, Guid solicitacao, Guid responsavel, string token) =>
        equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Patch, $"/api/v1/solicitacoes/{solicitacao}", new
        {
            titulo = "Já com responsável", prioridade = "ALTA", responsavelId = responsavel,
        }, token);

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
