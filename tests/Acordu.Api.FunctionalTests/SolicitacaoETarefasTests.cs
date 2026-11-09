using System.Net;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Acesso;

namespace Acordu.Api.FunctionalTests;

/// <summary>Spec do F11, F11c: solicitação e tarefas andam juntas (ações conjuntas e transições automáticas).</summary>
[Collection(ColecaoApi.Nome)]
public sealed class SolicitacaoETarefasTests(ApiFixture api)
{
    private sealed record Cenario(Equipe Equipe, Guid Solicitacao, Guid AFazer, Guid Fazendo)
    {
        public string Rota => $"/api/v1/solicitacoes/{Solicitacao}";
    }

    private static async Task<Cenario> CenarioAsync(ApiFixture api)
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var solicitacao = await Cenarios.SolicitacaoAsync(equipe, cliente);
        var aFazer = await Cenarios.TarefaAsync(equipe, solicitacao, "Trocar o cabo");
        var fazendo = await Cenarios.TarefaAsync(equipe, solicitacao, "Testar a impressora");
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/tarefas/{fazendo}/inicio");
        return new Cenario(equipe, solicitacao, aFazer, fazendo);
    }

    private static async Task<string> StatusAsync(Equipe equipe, string rota) =>
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, rota)).GetProperty("status").GetString()!;

    [Fact]
    public async Task Resolver_ConcluirTarefasJunto_TudoMuda() // F11-12
    {
        var c = await CenarioAsync(api);

        await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"{c.Rota}/resolucao", new
        {
            solucao = new { texto = "Cabo trocado" },
            tarefas = new { acao = "CONCLUIR", ids = new[] { c.AFazer, c.Fazendo } },
        });

        (await StatusAsync(c.Equipe, c.Rota)).Should().Be("RESOLVIDA");
        (await StatusAsync(c.Equipe, $"/api/v1/tarefas/{c.AFazer}")).Should().Be("CONCLUIDA");
        (await StatusAsync(c.Equipe, $"/api/v1/tarefas/{c.Fazendo}")).Should().Be("CONCLUIDA");
    }

    [Fact]
    public async Task Resolver_ManterTarefas_ComoHoje() // F11-13
    {
        var c = await CenarioAsync(api);

        await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"{c.Rota}/resolucao", new
        {
            solucao = new { texto = "Cabo trocado" }, tarefas = new { acao = "MANTER" },
        });

        (await StatusAsync(c.Equipe, c.Rota)).Should().Be("RESOLVIDA");
        (await StatusAsync(c.Equipe, $"/api/v1/tarefas/{c.AFazer}")).Should().Be("A_FAZER");
    }

    [Fact]
    public async Task Resolver_ConcluirSemTarefaEditar_403ENadaMuda() // F11-14
    {
        var c = await CenarioAsync(api);
        await c.Equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/papeis", new
        {
            nome = "Só solicitações", permissoes = new[] { Permissoes.SolicitacaoVer, Permissoes.SolicitacaoEditar },
        });
        var token = await c.Equipe.ComPapelAsync("Só solicitações");

        await c.Equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Post, $"{c.Rota}/resolucao", new
        {
            solucao = new { texto = "Cabo trocado" },
            tarefas = new { acao = "CONCLUIR", ids = new[] { c.AFazer, c.Fazendo } },
        }, token: token);

        (await StatusAsync(c.Equipe, c.Rota)).Should().NotBe("RESOLVIDA");
        await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"{c.Rota}/resolucao", new
        {
            solucao = new { texto = "Cabo trocado" },
        }, token: token);
        (await StatusAsync(c.Equipe, $"/api/v1/tarefas/{c.AFazer}")).Should().Be("A_FAZER", "sem ação conjunta, só a solicitação resolve");
    }

    [Fact]
    public async Task Resolver_ListaDeTarefasMudou_409ENadaMuda() // F11-31
    {
        var c = await CenarioAsync(api);
        var nova = await Cenarios.TarefaAsync(c.Equipe, c.Solicitacao, "Chegou depois");

        var problema = await c.Equipe.EsperarAsync(HttpStatusCode.Conflict, HttpMethod.Post, $"{c.Rota}/resolucao", new
        {
            solucao = new { texto = "Cabo trocado" },
            tarefas = new { acao = "CONCLUIR", ids = new[] { c.AFazer, c.Fazendo } },
        });

        problema.GetProperty("type").GetString().Should().EndWith("/conflito-versao");
        (await StatusAsync(c.Equipe, c.Rota)).Should().NotBe("RESOLVIDA");
        (await StatusAsync(c.Equipe, $"/api/v1/tarefas/{c.AFazer}")).Should().Be("A_FAZER");
        (await StatusAsync(c.Equipe, $"/api/v1/tarefas/{nova}")).Should().Be("A_FAZER");
    }

    [Fact]
    public async Task Resolver_AcaoDeCancelar_400NoCampo()
    {
        var c = await CenarioAsync(api);

        var problema = await c.Equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Post, $"{c.Rota}/resolucao", new
        {
            solucao = new { texto = "Cabo trocado" },
            tarefas = new { acao = "CANCELAR", ids = new[] { c.AFazer, c.Fazendo } },
        });

        problema.GetProperty("errors").TryGetProperty("tarefas.acao", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Cancelar_CancelarTarefasJunto() // F11-20
    {
        var c = await CenarioAsync(api);

        await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"{c.Rota}/cancelamento", new
        {
            motivo = "Cliente desistiu", tarefas = new { acao = "CANCELAR", ids = new[] { c.AFazer, c.Fazendo } },
        });

        (await StatusAsync(c.Equipe, c.Rota)).Should().Be("CANCELADA");
        (await StatusAsync(c.Equipe, $"/api/v1/tarefas/{c.AFazer}")).Should().Be("CANCELADA");
        (await StatusAsync(c.Equipe, $"/api/v1/tarefas/{c.Fazendo}")).Should().Be("CANCELADA");
    }

    [Fact]
    public async Task Cancelar_SolicitacaoRecusaDepoisDasTarefas_NadaMuda() // F11-12, F11-20: uma transação só
    {
        var c = await CenarioAsync(api);

        // As tarefas são canceladas em memória antes; o motivo em branco faz a solicitação recusar, e nada é gravado.
        await c.Equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Post, $"{c.Rota}/cancelamento", new
        {
            motivo = "   ", tarefas = new { acao = "CANCELAR", ids = new[] { c.AFazer, c.Fazendo } },
        });

        (await StatusAsync(c.Equipe, c.Rota)).Should().Be("EM_ANDAMENTO");
        (await StatusAsync(c.Equipe, $"/api/v1/tarefas/{c.AFazer}")).Should().Be("A_FAZER");
        (await StatusAsync(c.Equipe, $"/api/v1/tarefas/{c.Fazendo}")).Should().Be("FAZENDO");
    }

    [Fact]
    public async Task CriarTarefa_SolicitacaoAberta_PassaAEmAndamento() // F11-18
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var solicitacao = await Cenarios.SolicitacaoAsync(equipe, cliente);
        var rota = $"/api/v1/solicitacoes/{solicitacao}";
        (await StatusAsync(equipe, rota)).Should().Be("ABERTA");

        await Cenarios.TarefaAsync(equipe, solicitacao);

        (await StatusAsync(equipe, rota)).Should().Be("EM_ANDAMENTO");
    }

    [Fact]
    public async Task IniciarCronometro_TarefaAFazer_PassaAFazendo() // F11-19
    {
        var equipe = await Equipe.CriarAsync(api);
        var tarefa = await Cenarios.TarefaAsync(equipe);

        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/cronometro/inicio", new { tarefaId = tarefa });

        (await StatusAsync(equipe, $"/api/v1/tarefas/{tarefa}")).Should().Be("FAZENDO");
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/cronometro/parada");
    }

    [Fact]
    public async Task Resolver_TarefaDeOutroTenantNaLista_409SemRevelar() // F11-29
    {
        var c = await CenarioAsync(api);
        var outra = await CenarioAsync(api);

        await c.Equipe.EsperarAsync(HttpStatusCode.Conflict, HttpMethod.Post, $"{c.Rota}/resolucao", new
        {
            solucao = new { texto = "Cabo trocado" },
            tarefas = new { acao = "CONCLUIR", ids = new[] { c.AFazer, c.Fazendo, outra.AFazer } },
        });

        (await StatusAsync(outra.Equipe, $"/api/v1/tarefas/{outra.AFazer}")).Should().Be("A_FAZER");
        await c.Equipe.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Post, $"/api/v1/solicitacoes/{outra.Solicitacao}/resolucao",
            new { solucao = new { texto = "x" }, tarefas = new { acao = "MANTER" } });
    }
}
