using System.Net;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Domain.Entities;

namespace Acordu.Api.FunctionalTests;

/// <summary>Contrato §10: recorrências, transições, prévia, validações, permissões e isolamento.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class RecorrenciasTests(ApiFixture api)
{
    private const string Base = "https://acordu.app.br/erros/";

    /// <summary>Início em 2030: as datas não dependem do relógio compartilhado da fixture.</summary>
    private static object Nova(
        Guid clienteId, Guid? contratoId = null, Guid? responsavelId = null, int dia = 31, string periodicidade = "MENSAL",
        string inicio = "2030-01-01") =>
        new
        {
            clienteId,
            contratoId,
            modelo = new
            {
                titulo = "Fechamento mensal",
                responsavelId,
                estimativaMinutos = 240,
                visivelCliente = true,
                checklist = new[] { "Conciliar extratos", "Emitir guias" },
            },
            regra = new { periodicidade, diaReferencia = dia, antecedenciaDias = 3, inicio },
        };

    private static string[] Datas(JsonElement lista) => [.. lista.EnumerateArray().Select(d => d.GetString()!)];

    [Fact]
    public async Task Criar_DetalheComModeloRegraEProximaData()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contrato = await Cenarios.ContratoAsync(equipe, cliente);
        var eu = (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu")).GetProperty("usuarioId").GetGuid();

        var criada = await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, "/api/v1/recorrencias", Nova(cliente, contrato, eu));

        criada.GetProperty("clienteNome").GetString().Should().Be("Padaria Bom Grão");
        criada.GetProperty("contratoId").GetGuid().Should().Be(contrato);
        criada.GetProperty("status").GetString().Should().Be("ATIVA");
        criada.GetProperty("proximaData").GetString().Should().Be("2030-01-31");
        var modelo = criada.GetProperty("modelo");
        modelo.GetProperty("titulo").GetString().Should().Be("Fechamento mensal");
        modelo.GetProperty("responsavelId").GetGuid().Should().Be(eu);
        modelo.GetProperty("estimativaMinutos").GetInt32().Should().Be(240);
        Datas(modelo.GetProperty("checklist")).Should().Equal("Conciliar extratos", "Emitir guias");
        var regra = criada.GetProperty("regra");
        regra.GetProperty("periodicidade").GetString().Should().Be("MENSAL");
        regra.GetProperty("diaReferencia").GetInt32().Should().Be(31);
        regra.GetProperty("inicio").GetString().Should().Be("2030-01-01");
        criada.GetProperty("versao").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Previa_MostraOClampDeFimDeMes()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var id = Cenarios.Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/recorrencias", Nova(cliente)));

        var padrao = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/recorrencias/{id}/previa");
        var tres = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/recorrencias/{id}/previa?quantidade=3");
        var demais = await equipe.EsperarAsync(
            HttpStatusCode.BadRequest, HttpMethod.Get, $"/api/v1/recorrencias/{id}/previa?quantidade=100");

        Datas(padrao).Should().Equal("2030-01-31", "2030-02-28", "2030-03-31", "2030-04-30", "2030-05-31", "2030-06-30");
        Datas(tres).Should().HaveCount(3);
        demais.GetProperty("errors").TryGetProperty("quantidade", out _).Should().BeTrue();
    }

    [Fact]
    public async Task InicioNoPassado_SoAncoraOCiclo_SemDatasRetroativas()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);

        var criada = await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, "/api/v1/recorrencias",
            Nova(cliente, periodicidade: "SEMANAL", dia: 1, inicio: "2020-01-01"));

        DateOnly.Parse(criada.GetProperty("proximaData").GetString()!, System.Globalization.CultureInfo.InvariantCulture)
            .Should().BeOnOrAfter(new DateOnly(2026, 10, 1));
    }

    [Fact]
    public async Task Criar_Invalida_400NoCampo()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var outro = await Cenarios.ClienteAsync(equipe, "Contábil Silva");
        var contratoDoOutro = await Cenarios.ContratoAsync(equipe, outro);

        var dia = await equipe.EsperarAsync(
            HttpStatusCode.BadRequest, HttpMethod.Post, "/api/v1/recorrencias", Nova(cliente, periodicidade: "SEMANAL", dia: 9));
        var contrato = await equipe.EsperarAsync(
            HttpStatusCode.BadRequest, HttpMethod.Post, "/api/v1/recorrencias", Nova(cliente, contratoDoOutro));
        var responsavel = await equipe.EsperarAsync(
            HttpStatusCode.BadRequest, HttpMethod.Post, "/api/v1/recorrencias", Nova(cliente, responsavelId: Guid.NewGuid()));
        var semCliente = await equipe.EsperarAsync(
            HttpStatusCode.BadRequest, HttpMethod.Post, "/api/v1/recorrencias", Nova(Guid.NewGuid()));

        dia.GetProperty("type").GetString().Should().Be(Base + "validacao");
        dia.GetProperty("errors").TryGetProperty("regra.diaReferencia", out _).Should().BeTrue();
        contrato.GetProperty("errors").TryGetProperty("contratoId", out _).Should().BeTrue();
        responsavel.GetProperty("errors").TryGetProperty("modelo.responsavelId", out _).Should().BeTrue();
        semCliente.GetProperty("errors").TryGetProperty("clienteId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Editar_ModeloERegra_ComIfMatch()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var criada = await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/recorrencias", Nova(cliente));
        var versao = criada.GetProperty("versao").GetString();
        var edicao = new
        {
            contratoId = (Guid?)null,
            modelo = new { titulo = "Fechamento trimestral", visivelCliente = false, checklist = Array.Empty<string>() },
            regra = new { periodicidade = "TRIMESTRAL", diaReferencia = 10, antecedenciaDias = 0 },
        };

        var editada = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Patch, $"/api/v1/recorrencias/{Cenarios.Id(criada)}", edicao, ifMatch: versao);
        await equipe.EsperarAsync(
            HttpStatusCode.Conflict, HttpMethod.Patch, $"/api/v1/recorrencias/{Cenarios.Id(criada)}",
            new { edicao.contratoId, modelo = new { titulo = "Outro título", visivelCliente = false }, edicao.regra }, ifMatch: versao);

        editada.GetProperty("modelo").GetProperty("titulo").GetString().Should().Be("Fechamento trimestral");
        editada.GetProperty("modelo").GetProperty("checklist").GetArrayLength().Should().Be(0);
        // Início mantido (2030-01-01): ciclo jan/abr/jul/out.
        editada.GetProperty("regra").GetProperty("inicio").GetString().Should().Be("2030-01-01");
        editada.GetProperty("proximaData").GetString().Should().Be("2030-01-10");
    }

    [Fact]
    public async Task PausarRetomarEncerrar_Transicoes()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var id = Cenarios.Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/recorrencias", Nova(cliente)));

        var pausada = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/recorrencias/{id}/pausa");
        var previaPausada = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/recorrencias/{id}/previa");
        var retomada = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/recorrencias/{id}/retomada");
        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/v1/recorrencias/{id}");
        var encerrada = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/recorrencias/{id}");
        var previaEncerrada = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/recorrencias/{id}/previa");
        var invalida = await equipe.EsperarAsync(
            HttpStatusCode.UnprocessableEntity, HttpMethod.Post, $"/api/v1/recorrencias/{id}/retomada");

        pausada.GetProperty("status").GetString().Should().Be("PAUSADA");
        Datas(previaPausada)[0].Should().Be("2030-01-31");
        retomada.GetProperty("status").GetString().Should().Be("ATIVA");
        retomada.GetProperty("proximaData").GetString().Should().Be("2030-01-31");
        encerrada.GetProperty("status").GetString().Should().Be("ENCERRADA");
        previaEncerrada.GetArrayLength().Should().Be(0);
        invalida.GetProperty("type").GetString().Should().Be(Base + "transicao-invalida");
    }

    [Fact]
    public async Task Listar_FiltraPorClienteEStatus()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var outro = await Cenarios.ClienteAsync(equipe, "Contábil Silva");
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/recorrencias", Nova(cliente));
        var pausar = Cenarios.Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/recorrencias", Nova(outro)));
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/recorrencias/{pausar}/pausa");

        var todas = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/recorrencias");
        var doCliente = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/recorrencias?clienteId={cliente}");
        var pausadas = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/recorrencias?status=PAUSADA");

        todas.GetProperty("total").GetInt32().Should().Be(2);
        todas.GetProperty("itens")[0].GetProperty("status").GetString().Should().Be("ATIVA");
        doCliente.GetProperty("itens")[0].GetProperty("titulo").GetString().Should().Be("Fechamento mensal");
        doCliente.GetProperty("total").GetInt32().Should().Be(1);
        pausadas.GetProperty("itens")[0].GetProperty("id").GetGuid().Should().Be(pausar);
        pausadas.GetProperty("total").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Leitor_VeMasNaoGerencia_OutroTenantNaoVe_ClienteNaoAcessa()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var id = Cenarios.Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/recorrencias", Nova(cliente)));
        var leitor = await equipe.ComPapelAsync(Papel.NomeLeitor);
        var outra = await Equipe.CriarAsync(api);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);

        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/recorrencias/{id}", token: leitor);
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Post, $"/api/v1/recorrencias/{id}/pausa", token: leitor);
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Post, "/api/v1/recorrencias", Nova(cliente), token: leitor);
        await outra.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"/api/v1/recorrencias/{id}");
        await outra.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Delete, $"/api/v1/recorrencias/{id}");
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Get, "/api/v1/recorrencias", token: contato.Token);
    }
}
