using System.Net;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Acesso;
using static Acordu.Api.FunctionalTests.Apoio.Cenarios;

namespace Acordu.Api.FunctionalTests;

/// <summary>Spec do F11, F11e: projeto a partir da solicitação e projeto com as tarefas abertas (F11-21, 22, 27).</summary>
[Collection(ColecaoApi.Nome)]
public sealed class ProjetoETarefasTests(ApiFixture api)
{
    private static async Task<string?> StatusAsync(Equipe equipe, string rota) =>
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, rota)).GetProperty("status").GetString();

    private static async Task<Guid> ProjetoAsync(Equipe equipe, Guid cliente) =>
        Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/projetos", new
        {
            clienteId = cliente, nome = "Site novo",
        }));

    private static async Task<Guid> TarefaNoProjetoAsync(Equipe equipe, Guid projeto, string titulo) =>
        Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/tarefas", new
        {
            titulo, projetoId = projeto,
        }));

    [Fact]
    public async Task Criar_LevandoAsTarefasDaSolicitacao_PassamAoProjetoEContinuamNaSolicitacao() // F11-22
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var solicitacao = await SolicitacaoAsync(equipe, cliente);
        var arte = await TarefaAsync(equipe, solicitacao, "Arte");
        var texto = await TarefaAsync(equipe, solicitacao, "Texto");

        var projeto = Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/projetos", new
        {
            clienteId = cliente, nome = "Campanha", tarefaIds = new[] { arte, texto },
        }));

        foreach (var tarefa in new[] { arte, texto })
        {
            var dto = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/tarefas/{tarefa}");
            dto.GetProperty("projetoId").GetGuid().Should().Be(projeto);
            dto.GetProperty("solicitacaoId").GetGuid().Should().Be(solicitacao);
        }

        var detalhe = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}");
        detalhe.GetProperty("tarefas").EnumerateArray().Select(t => t.GetProperty("projetoId").GetGuid())
            .Should().AllBeEquivalentTo(projeto);
    }

    [Fact]
    public async Task Criar_TarefaDeOutroCliente_422ENadaECriado() // F11-22 com a regra do F11-24
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var oficina = await ClienteAsync(equipe, "Oficina");
        var daOficina = await TarefaAsync(equipe, await SolicitacaoAsync(equipe, oficina), "Vitrine");

        var problema = await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Post, "/api/v1/projetos", new
        {
            clienteId = cliente, nome = "Campanha", tarefaIds = new[] { daOficina },
        });

        problema.GetProperty("type").GetString().Should().EndWith("/projeto-de-outro-cliente");
        var lista = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/projetos?clienteId={cliente}");
        lista.GetProperty("itens").GetArrayLength().Should().Be(0, "o projeto e a mudança são uma transação só");
    }

    [Fact]
    public async Task Criar_LevandoTarefasSemTarefaEditar_403()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var arte = await TarefaAsync(equipe, await SolicitacaoAsync(equipe, cliente), "Arte");
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/papeis", new
        {
            nome = "Projetos", permissoes = new[] { Permissoes.ProjetoVer, Permissoes.ProjetoGerenciar, Permissoes.TarefaVer },
        });
        var token = await equipe.ComPapelAsync("Projetos");

        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Post, "/api/v1/projetos", new
        {
            clienteId = cliente, nome = "Campanha", tarefaIds = new[] { arte },
        }, token: token);

        (await ProjetosDoClienteAsync(equipe, cliente)).Should().Be(0);
    }

    private static async Task<int> ProjetosDoClienteAsync(Equipe equipe, Guid cliente) =>
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/projetos?clienteId={cliente}"))
            .GetProperty("itens").GetArrayLength();

    [Fact]
    public async Task Criar_TarefaCancelada_422ENadaECriado()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var tarefa = await TarefaAsync(equipe, await SolicitacaoAsync(equipe, cliente), "Arte");
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/tarefas/{tarefa}/cancelamento");

        var problema = await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Post, "/api/v1/projetos", new
        {
            clienteId = cliente, nome = "Campanha", tarefaIds = new[] { tarefa },
        });

        problema.GetProperty("type").GetString().Should().EndWith("/transicao-invalida");
        (await ProjetosDoClienteAsync(equipe, cliente)).Should().Be(0);
    }

    [Fact]
    public async Task Criar_TarefaInexistente_400ENadaECriado()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);

        var problema = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Post, "/api/v1/projetos", new
        {
            clienteId = cliente, nome = "Campanha", tarefaIds = new[] { Guid.NewGuid() },
        });

        problema.GetProperty("errors").TryGetProperty("tarefaIds", out _).Should().BeTrue();
        (await ProjetosDoClienteAsync(equipe, cliente)).Should().Be(0);
    }

    [Fact]
    public async Task Criar_TarefaDeOutroTenant_400SemRevelarENadaECriado() // F11-29
    {
        var equipe = await Equipe.CriarAsync(api);
        var outra = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        var alheia = await TarefaAsync(outra, await SolicitacaoAsync(outra, await ClienteAsync(outra)), "Alheia");

        var problema = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Post, "/api/v1/projetos", new
        {
            clienteId = cliente, nome = "Campanha", tarefaIds = new[] { alheia },
        });

        problema.GetProperty("errors").GetProperty("tarefaIds")[0].GetString().Should().Be("Tarefa não encontrada.");
        (await ProjetosDoClienteAsync(equipe, cliente)).Should().Be(0);
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/tarefas")).GetProperty("itens").GetArrayLength()
            .Should().Be(0);
        (await outra.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/tarefas/{alheia}"))
            .GetProperty("projetoId").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task Concluir_TarefaDeOutroTenantNaLista_409SemRevelar() // F11-29
    {
        var equipe = await Equipe.CriarAsync(api);
        var outra = await Equipe.CriarAsync(api);
        var projeto = await ProjetoAsync(equipe, await ClienteAsync(equipe));
        var nossa = await TarefaNoProjetoAsync(equipe, projeto, "Layout");
        var alheia = await TarefaAsync(outra, await SolicitacaoAsync(outra, await ClienteAsync(outra)), "Alheia");

        await equipe.EsperarAsync(HttpStatusCode.Conflict, HttpMethod.Post, $"/api/v1/projetos/{projeto}/conclusao", new
        {
            tarefas = new { acao = "CONCLUIR", ids = new[] { nossa, alheia } },
        });

        (await StatusAsync(equipe, $"/api/v1/projetos/{projeto}")).Should().Be("EM_ANDAMENTO");
        (await StatusAsync(outra, $"/api/v1/tarefas/{alheia}")).Should().Be("A_FAZER");
    }

    [Fact]
    public async Task Concluir_ComAsTarefasJunto_TudoMuda() // F11-27
    {
        var equipe = await Equipe.CriarAsync(api);
        var projeto = await ProjetoAsync(equipe, await ClienteAsync(equipe));
        var aFazer = await TarefaNoProjetoAsync(equipe, projeto, "Layout");
        var fazendo = await TarefaNoProjetoAsync(equipe, projeto, "Textos");
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/tarefas/{fazendo}/inicio");

        var concluido = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/projetos/{projeto}/conclusao", new
        {
            tarefas = new { acao = "CONCLUIR", ids = new[] { aFazer, fazendo } },
        });

        concluido.GetProperty("status").GetString().Should().Be("CONCLUIDO");
        concluido.GetProperty("progresso").GetProperty("tarefasConcluidas").GetInt32().Should().Be(2);
        (await StatusAsync(equipe, $"/api/v1/tarefas/{aFazer}")).Should().Be("CONCLUIDA");
        (await StatusAsync(equipe, $"/api/v1/tarefas/{fazendo}")).Should().Be("CONCLUIDA");
    }

    [Fact]
    public async Task Cancelar_ComAsTarefasJunto_CancelaTodas() // F11-27
    {
        var equipe = await Equipe.CriarAsync(api);
        var projeto = await ProjetoAsync(equipe, await ClienteAsync(equipe));
        var tarefa = await TarefaNoProjetoAsync(equipe, projeto, "Layout");

        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/projetos/{projeto}/cancelamento", new
        {
            tarefas = new { acao = "CANCELAR", ids = new[] { tarefa } },
        });

        (await StatusAsync(equipe, $"/api/v1/tarefas/{tarefa}")).Should().Be("CANCELADA");
    }

    [Fact]
    public async Task Concluir_SemCorpo_TarefasFicamComoEstao()
    {
        var equipe = await Equipe.CriarAsync(api);
        var projeto = await ProjetoAsync(equipe, await ClienteAsync(equipe));
        var tarefa = await TarefaNoProjetoAsync(equipe, projeto, "Layout");

        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/projetos/{projeto}/conclusao");

        (await StatusAsync(equipe, $"/api/v1/tarefas/{tarefa}")).Should().Be("A_FAZER");
    }

    [Fact]
    public async Task Concluir_ListaMudou_409ENadaMuda() // F11-27 com a regra do F11-31
    {
        var equipe = await Equipe.CriarAsync(api);
        var projeto = await ProjetoAsync(equipe, await ClienteAsync(equipe));
        var vista = await TarefaNoProjetoAsync(equipe, projeto, "Layout");
        await TarefaNoProjetoAsync(equipe, projeto, "Nova enquanto a janela estava aberta");

        await equipe.EsperarAsync(HttpStatusCode.Conflict, HttpMethod.Post, $"/api/v1/projetos/{projeto}/conclusao", new
        {
            tarefas = new { acao = "CONCLUIR", ids = new[] { vista } },
        });

        (await StatusAsync(equipe, $"/api/v1/projetos/{projeto}")).Should().Be("EM_ANDAMENTO");
        (await StatusAsync(equipe, $"/api/v1/tarefas/{vista}")).Should().Be("A_FAZER");
    }

    [Fact]
    public async Task Concluir_AcaoDeCancelar_400()
    {
        var equipe = await Equipe.CriarAsync(api);
        var projeto = await ProjetoAsync(equipe, await ClienteAsync(equipe));
        var tarefa = await TarefaNoProjetoAsync(equipe, projeto, "Layout");

        var problema = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Post, $"/api/v1/projetos/{projeto}/conclusao", new
        {
            tarefas = new { acao = "CANCELAR", ids = new[] { tarefa } },
        });

        problema.GetProperty("errors").TryGetProperty("tarefas.acao", out _).Should().BeTrue();
        (await StatusAsync(equipe, $"/api/v1/projetos/{projeto}")).Should().Be("EM_ANDAMENTO");
    }

    [Fact]
    public async Task Concluir_ComTarefasSemTarefaEditar_403ENadaMuda()
    {
        var equipe = await Equipe.CriarAsync(api);
        var projeto = await ProjetoAsync(equipe, await ClienteAsync(equipe));
        var tarefa = await TarefaNoProjetoAsync(equipe, projeto, "Layout");
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/papeis", new
        {
            nome = "Projetos", permissoes = new[] { Permissoes.ProjetoVer, Permissoes.ProjetoGerenciar, Permissoes.TarefaVer },
        });
        var token = await equipe.ComPapelAsync("Projetos");

        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Post, $"/api/v1/projetos/{projeto}/conclusao", new
        {
            tarefas = new { acao = "CONCLUIR", ids = new[] { tarefa } },
        }, token: token);

        (await StatusAsync(equipe, $"/api/v1/projetos/{projeto}")).Should().Be("EM_ANDAMENTO");
    }
}
