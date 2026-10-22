using System.Net;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Entities;
using static Acordu.Api.FunctionalTests.Apoio.Cenarios;

namespace Acordu.Api.FunctionalTests;

/// <summary>Contrato §6: calendário, configurações do portal e categorias.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class ConfiguracoesTests(ApiFixture api)
{
    private static object Calendario(string fuso = "America/Sao_Paulo", string fim = "18:00") => new
    {
        fusoHorario = fuso,
        expediente = new[]
        {
            new { diaSemana = "SEGUNDA", inicio = "08:00", fim },
            new { diaSemana = "TERCA", inicio = "08:00", fim },
            new { diaSemana = "QUARTA", inicio = "08:00", fim },
            new { diaSemana = "QUINTA", inicio = "08:00", fim },
            new { diaSemana = "SEXTA", inicio = "08:00", fim = "17:00" },
        },
        feriados = new[] { "2026-11-02", "2026-10-12", "2026-11-02" },
    };

    private static object[] Sla(int urgenteResolucao = 360) =>
    [
        new { prioridade = "URGENTE", primeiraRespostaMinutos = 60, resolucaoMinutos = urgenteResolucao },
        new { prioridade = "ALTA", primeiraRespostaMinutos = 120, resolucaoMinutos = 960 },
        new { prioridade = "MEDIA", primeiraRespostaMinutos = 240, resolucaoMinutos = 1920 },
        new { prioridade = "BAIXA", primeiraRespostaMinutos = 480, resolucaoMinutos = 4800 },
    ];

    [Fact]
    public async Task Empresa_SugereOSlaDoSistema_DepoisODaEmpresa_EResponderParaNormalizado()
    {
        var equipe = await Equipe.CriarAsync(api);
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);

        var padrao = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/configuracoes/empresa", token: membro);
        var salvo = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, "/api/v1/configuracoes/empresa", new
        {
            nome = "  Estúdio Traço Design ", responderPara = " Atendimento@EstudioTraco.com.br ", slaPadrao = Sla(),
        });
        var relido = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/configuracoes/empresa", token: membro);

        padrao.GetProperty("slaPersonalizado").GetBoolean().Should().BeFalse();
        padrao.GetProperty("responderPara").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        padrao.GetProperty("slaPadrao")[0].GetProperty("prioridade").GetString().Should().Be("URGENTE");
        padrao.GetProperty("slaPadrao")[0].GetProperty("resolucaoMinutos").GetInt32().Should().Be(240);
        salvo.GetProperty("nome").GetString().Should().Be("Estúdio Traço Design");
        salvo.GetProperty("responderPara").GetString().Should().Be("atendimento@estudiotraco.com.br");
        relido.GetProperty("slaPersonalizado").GetBoolean().Should().BeTrue();
        relido.GetProperty("slaPadrao")[0].GetProperty("resolucaoMinutos").GetInt32().Should().Be(360);
    }

    [Fact]
    public async Task Empresa_ErrosNoCampo_EMembroNaoAltera()
    {
        var equipe = await Equipe.CriarAsync(api);
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);

        var email = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Put, "/api/v1/configuracoes/empresa", new
        {
            nome = "Estúdio", responderPara = "sem-arroba", slaPadrao = Sla(),
        });
        var nome = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Put, "/api/v1/configuracoes/empresa", new
        {
            nome = " ", responderPara = (string?)null, slaPadrao = Sla(),
        });
        var sla = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Put, "/api/v1/configuracoes/empresa", new
        {
            nome = "Estúdio", responderPara = (string?)null, slaPadrao = Sla(urgenteResolucao: 30),
        });
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Put, "/api/v1/configuracoes/empresa", new
        {
            nome = "Estúdio", responderPara = (string?)null, slaPadrao = Sla(),
        }, token: membro);

        email.GetProperty("errors").TryGetProperty("responderPara", out _).Should().BeTrue();
        nome.GetProperty("errors").TryGetProperty("nome", out _).Should().BeTrue();
        sla.GetProperty("errors").TryGetProperty("slaPadrao", out _).Should().BeTrue("resolução menor que a primeira resposta");
    }

    [Fact]
    public async Task Calendario_PadraoDoRegistro_EAlteracao()
    {
        var equipe = await Equipe.CriarAsync(api);

        var padrao = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/configuracoes/calendario");
        var alterado = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, "/api/v1/configuracoes/calendario", Calendario());
        var relido = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/configuracoes/calendario");

        padrao.GetProperty("fusoHorario").GetString().Should().Be("America/Sao_Paulo");
        padrao.GetProperty("expediente").GetArrayLength().Should().Be(5);
        padrao.GetProperty("expediente")[0].GetProperty("diaSemana").GetString().Should().Be("SEGUNDA");
        padrao.GetProperty("expediente")[0].GetProperty("inicio").GetString().Should().Be("08:00");
        alterado.GetProperty("expediente")[4].GetProperty("fim").GetString().Should().Be("17:00");
        relido.GetProperty("feriados").EnumerateArray().Select(f => f.GetString()).Should().Equal("2026-10-12", "2026-11-02");
    }

    [Fact]
    public async Task Calendario_EntradaInvalida400NoCampo_MembroLeMasNaoAltera()
    {
        var equipe = await Equipe.CriarAsync(api);
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);

        var fuso = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Put, "/api/v1/configuracoes/calendario", Calendario("Lua/Base"));
        var janela = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Put, "/api/v1/configuracoes/calendario", Calendario(fim: "07:00"));
        var vazio = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Put, "/api/v1/configuracoes/calendario", new
        {
            fusoHorario = "America/Sao_Paulo", expediente = Array.Empty<object>(),
        });
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/configuracoes/calendario", token: membro);
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Put, "/api/v1/configuracoes/calendario", Calendario(), token: membro);

        fuso.GetProperty("errors").TryGetProperty("fusoHorario", out _).Should().BeTrue();
        janela.GetProperty("errors").TryGetProperty("expediente", out _).Should().BeTrue();
        vazio.GetProperty("errors").TryGetProperty("expediente", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Calendario_NovaSolicitacaoUsaOExpedienteNovo_AntigaMantemOPrazo()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await ClienteAsync(equipe);
        await ContratoAsync(equipe, cliente);
        var antes = await SolicitacaoAsync(equipe, cliente);
        var prazoAntes = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{antes}"))
            .GetProperty("sla").GetProperty("prazoResolucao").GetDateTimeOffset();

        // Uma hora útil por dia: o mesmo SLA leva muito mais dias corridos.
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, "/api/v1/configuracoes/calendario", new
        {
            fusoHorario = "America/Sao_Paulo",
            expediente = new[] { new { diaSemana = "SEGUNDA", inicio = "08:00", fim = "09:00" } },
        });
        var depois = await SolicitacaoAsync(equipe, cliente);
        var prazoDepois = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{depois}"))
            .GetProperty("sla").GetProperty("prazoResolucao").GetDateTimeOffset();
        var antigaRelida = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{antes}"))
            .GetProperty("sla").GetProperty("prazoResolucao").GetDateTimeOffset();

        prazoDepois.Should().BeAfter(prazoAntes.AddDays(30));
        antigaRelida.Should().Be(prazoAntes, "mudar o calendário não recalcula prazos já fixados");
    }

    [Fact]
    public async Task Portal_LerEAlterar_DiasForaDaFaixa400()
    {
        var equipe = await Equipe.CriarAsync(api);
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);

        var padrao = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/configuracoes/portal");
        var alterado = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Put, "/api/v1/configuracoes/portal", new { nomeExibido = " Traço Design ", diasFechamentoAutomatico = 10 });
        var foraDaFaixa = await equipe.EsperarAsync(
            HttpStatusCode.BadRequest, HttpMethod.Put, "/api/v1/configuracoes/portal", new { diasFechamentoAutomatico = 0 });
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Get, "/api/v1/configuracoes/portal", token: membro);

        padrao.GetProperty("nomeTenant").GetString().Should().Be("Estúdio Traço");
        padrao.GetProperty("diasFechamentoAutomatico").GetInt32().Should().Be(5);
        alterado.GetProperty("nomeExibido").GetString().Should().Be("Traço Design");
        alterado.GetProperty("diasFechamentoAutomatico").GetInt32().Should().Be(10);
        foraDaFaixa.GetProperty("errors").TryGetProperty("diasFechamentoAutomatico", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Categorias_CrudNomeRepetidoEInativaRecusadaNaSolicitacao()
    {
        var equipe = await Equipe.CriarAsync(api);
        var outra = await Equipe.CriarAsync(api);
        var membro = await equipe.ComPapelAsync(Papel.NomeMembro);
        var cliente = await ClienteAsync(equipe);

        var design = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/categorias", new { nome = "Design" });
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/categorias", new { nome = "Suporte" });
        var repetido = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Post, "/api/v1/categorias", new { nome = "design" });
        var comCategoria = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/solicitacoes", new
        {
            clienteId = cliente, titulo = "Banner", prioridade = "MEDIA", categoriaId = Id(design),
        });
        var renomeada = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Patch, $"/api/v1/categorias/{Id(design)}", new { nome = "Design gráfico" });
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/v1/categorias/{Id(design)}");
        var inativa = await equipe.EsperarAsync(HttpStatusCode.BadRequest, HttpMethod.Post, "/api/v1/solicitacoes", new
        {
            clienteId = cliente, titulo = "Outro banner", prioridade = "MEDIA", categoriaId = Id(design),
        });
        var ativas = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/categorias?ativa=true", token: membro);
        var reativada = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Patch, $"/api/v1/categorias/{Id(design)}", new { nome = "Design gráfico", ativa = true });
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Post, "/api/v1/categorias", new { nome = "X" }, token: membro);
        await outra.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"/api/v1/categorias/{Id(design)}");

        repetido.GetProperty("errors").TryGetProperty("nome", out _).Should().BeTrue();
        comCategoria.GetProperty("categoriaId").GetGuid().Should().Be(Id(design));
        renomeada.GetProperty("nome").GetString().Should().Be("Design gráfico");
        inativa.GetProperty("errors").TryGetProperty("categoriaId", out _).Should().BeTrue();
        ativas.EnumerateArray().Select(c => c.GetProperty("nome").GetString()).Should().Equal("Suporte");
        reativada.GetProperty("ativa").GetBoolean().Should().BeTrue();
    }
}
