using System.Net;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;

namespace Acordu.Api.FunctionalTests;

/// <summary>Marco 12: troca de contrato da solicitação (com SLA e auditoria) e apontamento fora da vigência como avulso.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class ContratoDaSolicitacaoTests(ApiFixture api)
{
    private const string Erros = "https://acordu.app.br/erros/";

    private static async Task<Guid> MensalAsync(Equipe equipe, Guid cliente) =>
        Cenarios.Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/contratos", new
        {
            clienteId = cliente, tipo = "MENSAL_FIXO", inicio = "2026-09-01", franquiaMinutos = 600, sla = Cenarios.Sla(60, 120),
        }));

    private static async Task<Guid> SolicitacaoAsync(Equipe equipe, Guid cliente, Guid contrato) =>
        Cenarios.Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/solicitacoes", new
        {
            clienteId = cliente, contratoId = contrato, titulo = "Ajustar banner", prioridade = "MEDIA",
        }));

    [Fact]
    public async Task Trocar_RecalculaOSla_ERegistraNaAuditoria()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var banco = await Cenarios.ContratoAsync(equipe, cliente);
        var mensal = await MensalAsync(equipe, cliente);
        var solicitacao = await SolicitacaoAsync(equipe, cliente, banco);
        var antes = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}"))
            .GetProperty("sla").GetProperty("prazoResolucao").GetDateTimeOffset();

        var trocada = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Put, $"/api/v1/solicitacoes/{solicitacao}/contrato", new { contratoId = mensal });

        trocada.GetProperty("contratoId").GetGuid().Should().Be(mensal);
        trocada.GetProperty("sla").GetProperty("prazoResolucao").GetDateTimeOffset().Should().BeBefore(antes);
        var trilha = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auditoria?acao=solicitacao.contrato");
        var registro = trilha.GetProperty("itens")[0];
        registro.GetProperty("alvoId").GetGuid().Should().Be(solicitacao);
        registro.GetProperty("dados").GetProperty("de").GetGuid().Should().Be(banco);
        registro.GetProperty("dados").GetProperty("para").GetGuid().Should().Be(mensal);
    }

    [Fact]
    public async Task Remover_TiraOSla()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var solicitacao = await SolicitacaoAsync(equipe, cliente, await Cenarios.ContratoAsync(equipe, cliente));

        var sem = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Put, $"/api/v1/solicitacoes/{solicitacao}/contrato", new { contratoId = (Guid?)null });

        sem.GetProperty("contratoId").ValueKind.Should().Be(JsonValueKind.Null);
        sem.GetProperty("sla").ValueKind.Should().Be(JsonValueKind.Null);
        sem.GetProperty("situacaoSla").GetString().Should().Be("SEM_SLA");
    }

    [Fact]
    public async Task ContratoDeOutroCliente_OuResolvida_422()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var outro = await Cenarios.ClienteAsync(equipe, "Contábil Silva");
        var doOutro = await Cenarios.ContratoAsync(equipe, outro);
        var solicitacao = await SolicitacaoAsync(equipe, cliente, await Cenarios.ContratoAsync(equipe, cliente));

        var incoerente = await equipe.EsperarAsync(
            HttpStatusCode.UnprocessableEntity, HttpMethod.Put, $"/api/v1/solicitacoes/{solicitacao}/contrato", new { contratoId = doOutro });
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/resolucao",
            new { solucao = new { texto = "Feito" } });
        var resolvida = await equipe.EsperarAsync(
            HttpStatusCode.UnprocessableEntity, HttpMethod.Put, $"/api/v1/solicitacoes/{solicitacao}/contrato", new { contratoId = (Guid?)null });

        incoerente.GetProperty("type").GetString().Should().Be(Erros + "contrato-incoerente");
        resolvida.GetProperty("type").GetString().Should().Be(Erros + "transicao-invalida");
    }

    [Fact]
    public async Task Apontamento_ContratoSuspenso_EntraComoAvulso()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contrato = await Cenarios.ContratoAsync(equipe, cliente);
        var tarefa = await Cenarios.TarefaAsync(equipe, await SolicitacaoAsync(equipe, cliente, contrato));
        object Apontamento(string data) => new { tarefaId = tarefa, data, duracaoMinutos = 30, descricao = "Arte", faturavel = true };

        var vigente = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/apontamentos", Apontamento("2026-09-15"));
        var antesDoInicio = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/apontamentos", Apontamento("2026-08-20"));
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/contratos/{contrato}/suspensao");
        var suspenso = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/apontamentos", Apontamento("2026-09-16"));

        vigente.GetProperty("contratoId").GetGuid().Should().Be(contrato);
        antesDoInicio.GetProperty("contratoId").ValueKind.Should().Be(JsonValueKind.Null);
        suspenso.GetProperty("contratoId").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
