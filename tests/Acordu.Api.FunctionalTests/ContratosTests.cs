using System.Net;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Entities;

namespace Acordu.Api.FunctionalTests;

/// <summary>Contrato §5: contratos, transições, ajustes, consumo, fechamento, RF-CTR-02, permissões e isolamento.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class ContratosTests(ApiFixture api)
{
    private const string Base = "https://acordu.app.br/erros/";

    private static readonly object[] Sla =
    [
        new { prioridade = "URGENTE", primeiraRespostaMinutos = 60, resolucaoMinutos = 240 },
        new { prioridade = "ALTA", primeiraRespostaMinutos = 120, resolucaoMinutos = 960 },
        new { prioridade = "MEDIA", primeiraRespostaMinutos = 240, resolucaoMinutos = 1920 },
        new { prioridade = "BAIXA", primeiraRespostaMinutos = 480, resolucaoMinutos = 4800 },
    ];

    private static object NovoContrato(Guid clienteId, string tipo = "BANCO_HORAS", int? franquia = 1200, string inicio = "2026-09-01", string? fim = null) =>
        new { clienteId, tipo, inicio, fim, franquiaMinutos = franquia, sla = Sla };

    private static async Task<(Equipe Equipe, Guid Cliente)> CenarioAsync(ApiFixture api)
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/clientes", new { nome = "Padaria Bom Grão" });
        return (equipe, cliente.GetProperty("id").GetGuid());
    }

    private static Guid Id(JsonElement elemento) => elemento.GetProperty("id").GetGuid();

    [Fact]
    public async Task Criar_BancoDeHoras_DetalheComSlaEConsumoDoMes()
    {
        var (equipe, cliente) = await CenarioAsync(api);

        var contrato = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/contratos", NovoContrato(cliente));

        contrato.GetProperty("clienteNome").GetString().Should().Be("Padaria Bom Grão");
        contrato.GetProperty("tipo").GetString().Should().Be("BANCO_HORAS");
        contrato.GetProperty("status").GetString().Should().Be("ATIVO");
        contrato.GetProperty("inicio").GetString().Should().Be("2026-09-01");
        contrato.GetProperty("sla").GetArrayLength().Should().Be(4);
        contrato.GetProperty("periodosFechados").GetArrayLength().Should().Be(0);
        var consumo = contrato.GetProperty("consumoAtual");
        consumo.GetProperty("periodo").GetString().Should().Be("2026-10");
        consumo.GetProperty("saldoMinutos").GetInt32().Should().Be(1200);
        consumo.GetProperty("periodoFechado").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Criar_DadosIncoerentes_422Ou400()
    {
        var (equipe, cliente) = await CenarioAsync(api);

        var avulsoComFranquia = await equipe.EsperarAsync(
            HttpStatusCode.UnprocessableEntity, HttpMethod.Post, "/api/v1/contratos", NovoContrato(cliente, "AVULSO", 60));
        var slaIncompleto = await equipe.EsperarAsync(
            HttpStatusCode.UnprocessableEntity, HttpMethod.Post, "/api/v1/contratos",
            new { clienteId = cliente, tipo = "AVULSO", inicio = "2026-10-01", sla = Sla[..2] });
        var semCliente = await equipe.EsperarAsync(
            HttpStatusCode.BadRequest, HttpMethod.Post, "/api/v1/contratos", NovoContrato(Guid.NewGuid()));
        var fimAntes = await equipe.EsperarAsync(
            HttpStatusCode.BadRequest, HttpMethod.Post, "/api/v1/contratos", NovoContrato(cliente, fim: "2026-08-31"));

        avulsoComFranquia.GetProperty("type").GetString().Should().Be(Base + "contrato-incoerente");
        slaIncompleto.GetProperty("type").GetString().Should().Be(Base + "validacao");
        semCliente.GetProperty("errors").TryGetProperty("clienteId", out _).Should().BeTrue();
        fimAntes.GetProperty("errors").TryGetProperty("fim", out _).Should().BeTrue();
    }

    [Fact]
    public async Task UmAtivoPorTipo_SobrepostoRecusa_OutroTipoOuPeriodoAceita()
    {
        var (equipe, cliente) = await CenarioAsync(api);
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/contratos", NovoContrato(cliente, fim: "2026-12-31"));

        var sobreposto = await equipe.EsperarAsync(
            HttpStatusCode.UnprocessableEntity, HttpMethod.Post, "/api/v1/contratos", NovoContrato(cliente, inicio: "2026-12-01"));
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/contratos", NovoContrato(cliente, "MENSAL_FIXO", null));
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/contratos", NovoContrato(cliente, inicio: "2027-01-01"));

        sobreposto.GetProperty("type").GetString().Should().Be(Base + "contrato-incoerente");
    }

    [Fact]
    public async Task Reativar_ComOutroAtivoNoPeriodo_422()
    {
        var (equipe, cliente) = await CenarioAsync(api);
        var primeiro = Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/contratos", NovoContrato(cliente)));
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/contratos/{primeiro}/suspensao");
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/contratos", NovoContrato(cliente, inicio: "2026-10-01"));

        await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Post, $"/api/v1/contratos/{primeiro}/reativacao");
    }

    [Fact]
    public async Task Transicoes_SuspenderReativarEncerrar_EEncerradoNaoMuda()
    {
        var (equipe, cliente) = await CenarioAsync(api);
        var id = Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/contratos", NovoContrato(cliente)));

        var suspenso = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/contratos/{id}/suspensao");
        var reativado = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/contratos/{id}/reativacao");
        var encerrado = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/contratos/{id}/encerramento", new { data = "2026-10-31" });
        var invalida = await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Post, $"/api/v1/contratos/{id}/suspensao");

        suspenso.GetProperty("status").GetString().Should().Be("SUSPENSO");
        reativado.GetProperty("status").GetString().Should().Be("ATIVO");
        encerrado.GetProperty("status").GetString().Should().Be("ENCERRADO");
        encerrado.GetProperty("fim").GetString().Should().Be("2026-10-31");
        invalida.GetProperty("type").GetString().Should().Be(Base + "transicao-invalida");
    }

    [Fact]
    public async Task Editar_FranquiaFimESla_ComIfMatch()
    {
        var (equipe, cliente) = await CenarioAsync(api);
        var criado = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/contratos", NovoContrato(cliente));
        var versao = criado.GetProperty("versao").GetString();

        var editado = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Patch, $"/api/v1/contratos/{Id(criado)}",
            new { franquiaMinutos = 2400, fim = "2027-08-31", sla = Sla }, ifMatch: versao);
        await equipe.EsperarAsync(
            HttpStatusCode.Conflict, HttpMethod.Patch, $"/api/v1/contratos/{Id(criado)}",
            new { franquiaMinutos = 600, sla = Sla }, ifMatch: versao);

        editado.GetProperty("franquiaMinutos").GetInt32().Should().Be(2400);
        editado.GetProperty("fim").GetString().Should().Be("2027-08-31");
        editado.GetProperty("consumoAtual").GetProperty("saldoMinutos").GetInt32().Should().Be(2400);
    }

    [Fact]
    public async Task Ajuste_SomaNoSaldo_SoEmBancoDeHoras()
    {
        var (equipe, cliente) = await CenarioAsync(api);
        var banco = Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/contratos", NovoContrato(cliente)));
        var mensal = Id(await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, "/api/v1/contratos", NovoContrato(cliente, "MENSAL_FIXO", 600)));

        var ajuste = await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/contratos/{banco}/ajustes",
            new { quantidadeMinutos = 600, motivo = "Recarga de outubro", data = "2026-10-02" });
        var consumo = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/contratos/{banco}/consumo?periodo=2026-10");
        await equipe.EsperarAsync(
            HttpStatusCode.UnprocessableEntity, HttpMethod.Post, $"/api/v1/contratos/{mensal}/ajustes",
            new { quantidadeMinutos = 60, motivo = "Não pode" });

        ajuste.GetProperty("quantidadeMinutos").GetInt32().Should().Be(600);
        ajuste.GetProperty("autorId").GetGuid().Should().NotBe(Guid.Empty);
        consumo.GetProperty("ajustesMinutos").GetInt32().Should().Be(600);
        consumo.GetProperty("saldoMinutos").GetInt32().Should().Be(1800);
    }

    [Fact]
    public async Task Fechamento_MesEncerrado_MesCorrenteRecusa()
    {
        var (equipe, cliente) = await CenarioAsync(api);
        var id = Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/contratos", NovoContrato(cliente)));

        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Post, $"/api/v1/contratos/{id}/periodos/2026-09/fechamento");
        var corrente = await equipe.EsperarAsync(
            HttpStatusCode.BadRequest, HttpMethod.Post, $"/api/v1/contratos/{id}/periodos/2026-10/fechamento");
        var detalhe = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/contratos/{id}");
        var setembro = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/contratos/{id}/consumo?periodo=2026-09");

        corrente.GetProperty("errors").TryGetProperty("periodo", out _).Should().BeTrue();
        detalhe.GetProperty("periodosFechados").EnumerateArray().Select(p => p.GetString()).Should().Equal("2026-09");
        setembro.GetProperty("periodoFechado").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Listar_FiltraPorClienteEStatus()
    {
        var (equipe, cliente) = await CenarioAsync(api);
        var outroCliente = Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/clientes", new { nome = "Oficina" }));
        var suspenso = Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/contratos", NovoContrato(cliente)));
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/contratos/{suspenso}/suspensao");
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/contratos", NovoContrato(cliente, "AVULSO", null));
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/contratos", NovoContrato(outroCliente));

        var doCliente = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/contratos?clienteId={cliente}");
        var suspensos = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/contratos?status=SUSPENSO");
        await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Get, "/api/v1/contratos?status=QUALQUER");

        doCliente.GetProperty("total").GetInt32().Should().Be(2);
        suspensos.GetProperty("itens").EnumerateArray().Select(Id).Should().Equal(suspenso);
    }

    [Fact]
    public async Task Membro_VeMasNaoCria_OutroTenant404()
    {
        var (equipe, cliente) = await CenarioAsync(api);
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);
        var outra = await Equipe.CriarAsync(api);
        var id = Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/contratos", NovoContrato(cliente)));

        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/contratos/{id}", token: membro);
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Post, "/api/v1/contratos", NovoContrato(cliente, "AVULSO", null), token: membro);
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Post, $"/api/v1/contratos/{id}/periodos/2026-09/fechamento", token: membro);
        await outra.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"/api/v1/contratos/{id}");
        await outra.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"/api/v1/contratos/{id}/consumo");
    }
}
