using System.Net;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Entities;

namespace Acordu.Api.FunctionalTests;

/// <summary>Contrato §7: solicitações, transições, interações e a regra de primeira resposta (modelo §3.1).</summary>
[Collection(ColecaoApi.Nome)]
public sealed class SolicitacoesTests(ApiFixture api)
{
    private const string Base = "https://acordu.app.br/erros/";

    private static object[] Sla(int primeiraResposta, int resolucao) =>
    [
        new { prioridade = "URGENTE", primeiraRespostaMinutos = primeiraResposta, resolucaoMinutos = resolucao },
        new { prioridade = "ALTA", primeiraRespostaMinutos = primeiraResposta, resolucaoMinutos = resolucao },
        new { prioridade = "MEDIA", primeiraRespostaMinutos = primeiraResposta, resolucaoMinutos = resolucao },
        new { prioridade = "BAIXA", primeiraRespostaMinutos = primeiraResposta, resolucaoMinutos = resolucao },
    ];

    private static Guid Id(JsonElement elemento) => elemento.GetProperty("id").GetGuid();

    private static async Task<(Equipe Equipe, Guid Cliente)> ClienteAsync(ApiFixture api, int? primeiraResposta = 240, int resolucao = 1920)
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/clientes", new { nome = "Padaria Bom Grão" }));
        if (primeiraResposta is { } minutos)
        {
            await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/contratos", new
            {
                clienteId = cliente, tipo = "MENSAL_FIXO", inicio = "2026-09-01", sla = Sla(minutos, resolucao),
            });
        }

        return (equipe, cliente);
    }

    private static Task<JsonElement> AbrirAsync(Equipe equipe, Guid cliente, string titulo = "Ajustar banner da campanha", object? extra = null) =>
        equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/solicitacoes", extra ?? new
        {
            clienteId = cliente, titulo, descricao = "Trocar a data para 15/10.", prioridade = "MEDIA",
        });

    [Fact]
    public async Task Criar_UsaOContratoUnicoDoCliente_ComSlaEProtocolo()
    {
        var (equipe, cliente) = await ClienteAsync(api);

        var primeira = await AbrirAsync(equipe, cliente);
        var segunda = await AbrirAsync(equipe, cliente, "Outra coisa");

        primeira.GetProperty("numero").GetInt32().Should().Be(1);
        segunda.GetProperty("numero").GetInt32().Should().Be(2);
        primeira.GetProperty("status").GetString().Should().Be("ABERTA");
        primeira.GetProperty("statusExterno").GetString().Should().Be("RECEBIDA");
        primeira.GetProperty("origem").GetString().Should().Be("INTERNA");
        primeira.GetProperty("cliente").GetProperty("nome").GetString().Should().Be("Padaria Bom Grão");
        primeira.GetProperty("contratoId").ValueKind.Should().Be(JsonValueKind.String);
        primeira.GetProperty("sla").GetProperty("situacao").GetString().Should().Be("NO_PRAZO");
        primeira.GetProperty("situacaoSla").GetString().Should().Be("NO_PRAZO");
        primeira.GetProperty("tempoApontadoMinutos").GetInt32().Should().Be(0);
        primeira.GetProperty("versao").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Criar_SemContrato_SemSla()
    {
        var (equipe, cliente) = await ClienteAsync(api, primeiraResposta: null);

        var solicitacao = await AbrirAsync(equipe, cliente);

        solicitacao.GetProperty("sla").ValueKind.Should().Be(JsonValueKind.Null);
        solicitacao.GetProperty("situacaoSla").GetString().Should().Be("SEM_SLA");
    }

    [Fact]
    public async Task Criar_ContratoDeOutroCliente422_ClienteInexistente400()
    {
        var (equipe, cliente) = await ClienteAsync(api);
        var outro = Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/clientes", new { nome = "Oficina" }));
        var contratoDoOutro = Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/contratos", new
        {
            clienteId = outro, tipo = "AVULSO", inicio = "2026-09-01", sla = Sla(60, 120),
        }));

        var incoerente = await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Post, "/api/v1/solicitacoes", new
        {
            clienteId = cliente, contratoId = contratoDoOutro, titulo = "X", prioridade = "MEDIA",
        });
        var semCliente = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Post, "/api/v1/solicitacoes", new
        {
            clienteId = Guid.NewGuid(), titulo = "X", prioridade = "MEDIA",
        });
        var responsavelDeFora = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Post, "/api/v1/solicitacoes", new
        {
            clienteId = cliente, titulo = "X", prioridade = "MEDIA", responsavelId = Guid.NewGuid(),
        });

        incoerente.GetProperty("type").GetString().Should().Be(Base + "contrato-incoerente");
        semCliente.GetProperty("errors").TryGetProperty("clienteId", out _).Should().BeTrue();
        responsavelDeFora.GetProperty("errors").TryGetProperty("responsavelId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task CicloDeVida_TransicoesResolucaoReaberturaECancelamento()
    {
        var (equipe, cliente) = await ClienteAsync(api);
        var id = Id(await AbrirAsync(equipe, cliente));
        var rota = $"/api/v1/solicitacoes/{id}";

        var iniciada = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"{rota}/inicio");
        var aguardando = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"{rota}/aguardando-cliente");
        var retomada = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"{rota}/retomada");
        var semSolucao = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Post, $"{rota}/resolucao", new { });
        var resolvida = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"{rota}/resolucao", new
        {
            solucao = new { problema = "Data errada no banner.", causa = "Briefing antigo.", texto = "Arte refeita com a data 15/10." },
        });
        var reaberta = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"{rota}/reabertura", new { motivo = "Data ainda errada" });
        var cancelada = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"{rota}/cancelamento", new { motivo = "Cliente desistiu" });
        var invalida = await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Post, $"{rota}/inicio");

        iniciada.GetProperty("status").GetString().Should().Be("EM_ANDAMENTO");
        aguardando.GetProperty("statusExterno").GetString().Should().Be("AGUARDANDO_VOCE");
        aguardando.GetProperty("sla").GetProperty("pausado").GetBoolean().Should().BeTrue();
        retomada.GetProperty("sla").GetProperty("pausado").GetBoolean().Should().BeFalse();
        semSolucao.GetProperty("errors").TryGetProperty("solucao", out _).Should().BeTrue();
        resolvida.GetProperty("status").GetString().Should().Be("RESOLVIDA");
        resolvida.GetProperty("solucao").GetProperty("causa").GetString().Should().Be("Briefing antigo.");
        reaberta.GetProperty("status").GetString().Should().Be("EM_ANDAMENTO");
        reaberta.GetProperty("solucao").ValueKind.Should().Be(JsonValueKind.Null);
        cancelada.GetProperty("status").GetString().Should().Be("CANCELADA");
        cancelada.GetProperty("motivoCancelamento").GetString().Should().Be("Cliente desistiu");
        invalida.GetProperty("type").GetString().Should().Be(Base + "transicao-invalida");
    }

    [Fact]
    public async Task Editar_RepriorizaEAtribui_ComIfMatch()
    {
        var (equipe, cliente) = await ClienteAsync(api);
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);
        var membroId = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: membro)).GetProperty("usuarioId").GetGuid();
        var criada = await AbrirAsync(equipe, cliente);
        var prazoAntes = criada.GetProperty("sla").GetProperty("prazoResolucao").GetDateTimeOffset();

        var editada = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Patch, $"/api/v1/solicitacoes/{Id(criada)}", new
        {
            titulo = "Ajustar banner (urgente)", prioridade = "URGENTE", responsavelId = membroId,
        }, ifMatch: criada.GetProperty("versao").GetString());
        await equipe.EsperarAsync(HttpStatusCode.Conflict, HttpMethod.Patch, $"/api/v1/solicitacoes/{Id(criada)}", new
        {
            titulo = "Outro", prioridade = "BAIXA",
        }, ifMatch: criada.GetProperty("versao").GetString());

        editada.GetProperty("titulo").GetString().Should().Be("Ajustar banner (urgente)");
        editada.GetProperty("prioridade").GetString().Should().Be("URGENTE");
        editada.GetProperty("responsavel").GetProperty("id").GetGuid().Should().Be(membroId);
        editada.GetProperty("responsavel").GetProperty("nome").GetString().Should().Be("Pessoa da equipe");
        editada.GetProperty("descricao").ValueKind.Should().Be(JsonValueKind.Null, "o PATCH recebe o formulário inteiro");
        editada.GetProperty("sla").GetProperty("prazoResolucao").GetDateTimeOffset().Should().Be(prazoAntes,
            "o contrato de teste tem os mesmos prazos em todas as prioridades");
    }

    [Fact]
    public async Task Interacoes_ExternaDaEquipeRegistraPrimeiraResposta_InternaNao()
    {
        var (equipe, cliente) = await ClienteAsync(api);
        var id = Id(await AbrirAsync(equipe, cliente));
        var rota = $"/api/v1/solicitacoes/{id}";

        var interna = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, $"{rota}/interacoes", new { texto = "Vou olhar amanhã." });
        var depoisDaInterna = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, rota);
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, $"{rota}/interacoes", new { texto = "Recebido, já estamos ajustando.", visibilidade = "EXTERNA" });
        var depoisDaExterna = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, rota);
        var vazia = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Post, $"{rota}/interacoes", new { texto = "  " });
        var linha = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"{rota}/interacoes");

        interna.GetProperty("visibilidade").GetString().Should().Be("INTERNA");
        interna.GetProperty("autor").GetProperty("nome").GetString().Should().Be("Pessoa da equipe");
        depoisDaInterna.GetProperty("sla").GetProperty("primeiraRespostaEm").ValueKind.Should().Be(JsonValueKind.Null);
        depoisDaExterna.GetProperty("sla").GetProperty("primeiraRespostaEm").ValueKind.Should().Be(JsonValueKind.String);
        vazia.GetProperty("errors").TryGetProperty("texto", out _).Should().BeTrue();
        linha.EnumerateArray().Select(i => i.GetProperty("visibilidade").GetString()).Should().Equal("INTERNA", "EXTERNA");
    }

    [Fact]
    public async Task Listar_FiltrosEBusca()
    {
        var (equipe, cliente) = await ClienteAsync(api);
        var banner = await AbrirAsync(equipe, cliente, "Banner com data errada");
        var site = Id(await AbrirAsync(equipe, cliente, "Site fora do ar"));
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{site}/inicio");

        var abertas = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/solicitacoes?status=ABERTA");
        var ambas = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/solicitacoes?status=ABERTA,EM_ANDAMENTO&ordem=numero");
        var porTexto = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/solicitacoes?busca=banner");
        var porNumero = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/solicitacoes?busca=%232");
        await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Get, "/api/v1/solicitacoes?ordem=titulo");

        abertas.GetProperty("itens").EnumerateArray().Select(Id).Should().Equal(Id(banner));
        ambas.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("numero").GetInt32()).Should().Equal(1, 2);
        porTexto.GetProperty("total").GetInt32().Should().Be(1);
        porNumero.GetProperty("itens")[0].GetProperty("id").GetGuid().Should().Be(site);
    }

    [Fact]
    public async Task Listar_PorSituacaoDeSla_EmRiscoEDepoisViolada()
    {
        // Prazos curtos (minutos úteis): 2 de primeira resposta, 4 de resolução; risco a partir de 3.
        var (equipe, cliente) = await ClienteAsync(api, primeiraResposta: 2, resolucao: 4);
        var id = Id(await AbrirAsync(equipe, cliente));
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/solicitacoes/{id}/interacoes", new { texto = "Recebido.", visibilidade = "EXTERNA" });

        api.Relogio.Agora = api.Relogio.Agora.AddMinutes(3);
        var emRisco = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/solicitacoes?situacaoSla=EM_RISCO");
        api.Relogio.Agora = api.Relogio.Agora.AddMinutes(2);
        var violadas = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/solicitacoes?situacaoSla=VIOLADO");
        var semRisco = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/solicitacoes?situacaoSla=EM_RISCO");
        await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Get, "/api/v1/solicitacoes?situacaoSla=NO_PRAZO");

        emRisco.GetProperty("itens").EnumerateArray().Select(Id).Should().Equal(id);
        emRisco.GetProperty("itens")[0].GetProperty("situacaoSla").GetString().Should().Be("EM_RISCO");
        violadas.GetProperty("itens").EnumerateArray().Select(Id).Should().Equal(id);
        semRisco.GetProperty("total").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Semelhantes_DevolveLista()
    {
        var (equipe, cliente) = await ClienteAsync(api);
        var id = Id(await AbrirAsync(equipe, cliente));

        var semelhantes = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{id}/semelhantes");

        semelhantes.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task OutroTenant404_LeitorNaoCria()
    {
        var (equipe, cliente) = await ClienteAsync(api);
        var outra = await Equipe.CriarAsync(api);
        var leitor = await equipe.ComPapelAsync(Papel.NomeLeitor);
        var id = Id(await AbrirAsync(equipe, cliente));

        await outra.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"/api/v1/solicitacoes/{id}");
        await outra.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Post, $"/api/v1/solicitacoes/{id}/inicio");
        await outra.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"/api/v1/solicitacoes/{id}/interacoes");
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{id}", token: leitor);
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Post, "/api/v1/solicitacoes", new
        {
            clienteId = cliente, titulo = "X", prioridade = "MEDIA",
        }, token: leitor);
    }
}
