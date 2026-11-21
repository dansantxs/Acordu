using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Jobs;
using Acordu.Domain.Entities;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F14, F14l (F14-65 a F14-69, F14-71, F14-106 a F14-108; design D5 e D14; ADR 0014, itens 5 e 6; ADR 0015, item
/// 2): as ações da Nebrys sobre a situação das empresas, com <c>If-Match</c>, e a liberação de documento, com a trilha sem
/// tenant.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class AcoesNebrysTests(ApiFixture api)
{
    private const string Area = "/api/v1/nebrys";
    private static readonly TimeSpan PrazoAteExpurgo = TimeSpan.FromDays(30);
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    // ---------- F14-65: ativar ----------

    [Fact]
    public async Task Ativar_VencidaComAVersao_FicaAtivaEAGravacaoVoltaNaRequisicaoSeguinte() // F14-65
    {
        var equipe = await Equipe.CriarAsync(api);
        await GravarSituacaoAsync(equipe.TenantId, "VENCIDA", api.Relogio.Agora - TimeSpan.FromDays(2), null);
        await EsperarRecusaDeSoLeituraAsync(equipe);
        var token = await OperadorNebrys.TokenAsync(api);
        var versao = await VersaoAsync(token, equipe.TenantId);

        var (status, corpo) = await AgirAsync(token, equipe.TenantId, "ativacao", versao);

        status.Should().Be(HttpStatusCode.OK, "corpo: {0}", corpo);
        corpo.GetProperty("situacao").GetString().Should().Be("ATIVA");
        corpo.GetProperty("fimDoTeste").ValueKind.Should().Be(JsonValueKind.Null);
        corpo.GetProperty("somenteLeituraDesde").ValueKind.Should().Be(JsonValueKind.Null);
        corpo.GetProperty("expurgoPrevistoEm").ValueKind.Should().Be(JsonValueKind.Null);
        corpo.GetProperty("versao").GetString().Should().NotBe(versao);
        await Cenarios.ClienteAsync(equipe, "Cliente depois da ativação");
        (await SituacaoNoEuAsync(equipe)).Should().Be("ATIVA");
        (await GravavelDesdeAsync(equipe.TenantId)).Should().Be(api.Relogio.Agora, "a volta do só leitura fica gravada (F14-24)");
    }

    [Fact]
    public async Task Ativar_EmTesteSemIfMatch_FicaAtivaSemFimDoTeste() // F14-65
    {
        var equipe = await Equipe.CriarAsync(api);
        var token = await OperadorNebrys.TokenAsync(api);

        var (status, corpo) = await AgirAsync(token, equipe.TenantId, "ativacao", versao: null);

        status.Should().Be(HttpStatusCode.OK, "corpo: {0}", corpo);
        corpo.GetProperty("situacao").GetString().Should().Be("ATIVA");
        corpo.GetProperty("fimDoTeste").ValueKind.Should().Be(JsonValueKind.Null);
        (await GravavelDesdeAsync(equipe.TenantId)).Should().BeNull("a empresa em teste não estava só leitura");
        await Cenarios.ClienteAsync(equipe);
    }

    // ---------- F14-66: prorrogar ----------

    [Fact]
    public async Task Prorrogar_VencidaParaDataFutura_VoltaAoTesteComONovoFimEGrava() // F14-66
    {
        var equipe = await Equipe.CriarAsync(api);
        await GravarSituacaoAsync(equipe.TenantId, "VENCIDA", api.Relogio.Agora - TimeSpan.FromDays(1), null);
        var token = await OperadorNebrys.TokenAsync(api);
        var ate = Hoje().AddDays(10);

        var (status, corpo) = await AgirAsync(
            token, equipe.TenantId, "prorrogacao", await VersaoAsync(token, equipe.TenantId), new { ate = Dia(ate) });

        status.Should().Be(HttpStatusCode.OK, "corpo: {0}", corpo);
        corpo.GetProperty("situacao").GetString().Should().Be("TESTE");
        corpo.GetProperty("fimDoTeste").GetDateTimeOffset().Should().Be(InicioDoDia(ate.AddDays(1)));
        corpo.GetProperty("somenteLeituraDesde").ValueKind.Should().Be(JsonValueKind.Null);
        await Cenarios.ClienteAsync(equipe, "Cliente depois da prorrogação");
        (await GravavelDesdeAsync(equipe.TenantId)).Should().Be(api.Relogio.Agora);
    }

    [Fact]
    public async Task Prorrogar_EmTeste_TrocaOFimSemMarcarAVolta() // F14-66
    {
        var equipe = await Equipe.CriarAsync(api);
        var token = await OperadorNebrys.TokenAsync(api);
        var ate = Hoje().AddDays(60);

        var (status, corpo) = await AgirAsync(token, equipe.TenantId, "prorrogacao", null, new { ate = Dia(ate) });

        status.Should().Be(HttpStatusCode.OK, "corpo: {0}", corpo);
        corpo.GetProperty("situacao").GetString().Should().Be("TESTE");
        corpo.GetProperty("fimDoTeste").GetDateTimeOffset().Should().Be(InicioDoDia(ate.AddDays(1)));
        (await GravavelDesdeAsync(equipe.TenantId)).Should().BeNull();
    }

    [Fact]
    public async Task Prorrogar_SemODia_400NoCampo()
    {
        var equipe = await Equipe.CriarAsync(api);
        var token = await OperadorNebrys.TokenAsync(api);

        var (status, corpo) = await AgirAsync(token, equipe.TenantId, "prorrogacao", null, new { });

        status.Should().Be(HttpStatusCode.BadRequest, "corpo: {0}", corpo);
        corpo.GetProperty("errors").TryGetProperty("ate", out _).Should().BeTrue();
    }

    // ---------- F14-67: cancelar ----------

    [Theory] // F14-67
    [InlineData("TESTE")]
    [InlineData("ATIVA")]
    public async Task Cancelar_EmTesteOuAtiva_CanceladaSoLeituraComADataEOExpurgoPrevisto(string situacao)
    {
        var equipe = await Equipe.CriarAsync(api);
        if (situacao == "ATIVA")
            await GravarSituacaoAsync(equipe.TenantId, "ATIVA", null, null);
        var token = await OperadorNebrys.TokenAsync(api);
        var agora = api.Relogio.Agora;

        var (status, corpo) = await AgirAsync(token, equipe.TenantId, "cancelamento", await VersaoAsync(token, equipe.TenantId));

        status.Should().Be(HttpStatusCode.OK, "corpo: {0}", corpo);
        corpo.GetProperty("situacao").GetString().Should().Be("CANCELADA");
        corpo.GetProperty("canceladaEm").GetDateTimeOffset().Should().Be(agora);
        corpo.GetProperty("somenteLeituraDesde").GetDateTimeOffset().Should().Be(agora);
        corpo.GetProperty("expurgoPrevistoEm").GetDateTimeOffset().Should().Be(agora + PrazoAteExpurgo);
        await EsperarRecusaDeSoLeituraAsync(equipe);
    }

    [Fact]
    public async Task Cancelar_Vencida_MantemOInicioDoSoLeituraEGravaOCancelamento() // F14-67 (design §1.2)
    {
        var equipe = await Equipe.CriarAsync(api);
        var vencimento = api.Relogio.Agora - TimeSpan.FromDays(5);
        await GravarSituacaoAsync(equipe.TenantId, "VENCIDA", vencimento, null);
        var token = await OperadorNebrys.TokenAsync(api);

        var (status, corpo) = await AgirAsync(token, equipe.TenantId, "cancelamento", null);

        status.Should().Be(HttpStatusCode.OK, "corpo: {0}", corpo);
        corpo.GetProperty("situacao").GetString().Should().Be("CANCELADA");
        corpo.GetProperty("canceladaEm").GetDateTimeOffset().Should().Be(api.Relogio.Agora);
        corpo.GetProperty("somenteLeituraDesde").GetDateTimeOffset().Should().Be(vencimento);
        corpo.GetProperty("expurgoPrevistoEm").GetDateTimeOffset().Should().Be(vencimento + PrazoAteExpurgo);
    }

    // ---------- F14-68: reativar ----------

    [Fact]
    public async Task Reativar_Cancelada_FicaAtivaEGrava() // F14-68
    {
        var equipe = await Equipe.CriarAsync(api);
        var cancelamento = api.Relogio.Agora - TimeSpan.FromDays(3);
        await GravarSituacaoAsync(equipe.TenantId, "CANCELADA", cancelamento, cancelamento);
        await EsperarRecusaDeSoLeituraAsync(equipe);
        var token = await OperadorNebrys.TokenAsync(api);

        var (status, corpo) = await AgirAsync(token, equipe.TenantId, "reativacao", await VersaoAsync(token, equipe.TenantId));

        status.Should().Be(HttpStatusCode.OK, "corpo: {0}", corpo);
        corpo.GetProperty("situacao").GetString().Should().Be("ATIVA");
        corpo.GetProperty("canceladaEm").ValueKind.Should().Be(JsonValueKind.Null);
        corpo.GetProperty("somenteLeituraDesde").ValueKind.Should().Be(JsonValueKind.Null);
        corpo.GetProperty("expurgoPrevistoEm").ValueKind.Should().Be(JsonValueKind.Null);
        await Cenarios.ClienteAsync(equipe, "Cliente depois da reativação");
        (await GravavelDesdeAsync(equipe.TenantId)).Should().Be(api.Relogio.Agora);
    }

    // ---------- F14-69: transição inválida ----------

    public static TheoryData<string, string, string?, string> Invalidas => new()
    {
        // situação gravada, ação, corpo (dia da prorrogação, relativo a hoje), trecho da explicação
        { "ATIVA", "ativacao", null, "já está ativa" },
        { "CANCELADA", "cancelamento", null, "já está cancelada" },
        { "CANCELADA", "ativacao", null, "reativação" },
        { "ATIVA", "reativacao", null, "Só uma empresa cancelada" },
        { "TESTE", "reativacao", null, "Só uma empresa cancelada" },
        { "ATIVA", "prorrogacao", "+10", "em teste ou vencida" },
        { "VENCIDA", "prorrogacao", "-1", "hoje ou uma data futura" },
        { "TESTE", "prorrogacao", "-1", "hoje ou uma data futura" },
    };

    [Theory] // F14-69
    [MemberData(nameof(Invalidas))]
    public async Task Acao_TransicaoQueNaoValeNaSituacao_422ComAExplicacaoENadaMuda(
        string situacao, string acao, string? dias, string explicacao)
    {
        var equipe = await Equipe.CriarAsync(api);
        var desde = situacao is "VENCIDA" or "CANCELADA" ? api.Relogio.Agora - TimeSpan.FromDays(1) : (DateTimeOffset?)null;
        await GravarSituacaoAsync(equipe.TenantId, situacao, desde, situacao == "CANCELADA" ? desde : null);
        var token = await OperadorNebrys.TokenAsync(api);
        var antes = await DetalheAsync(token, equipe.TenantId);
        var corpoDaAcao = dias is null ? null : new { ate = Dia(Hoje().AddDays(int.Parse(dias, System.Globalization.CultureInfo.InvariantCulture))) };

        var (status, corpo) = await AgirAsync(token, equipe.TenantId, acao, antes.GetProperty("versao").GetString(), corpoDaAcao);

        status.Should().Be((HttpStatusCode)422, "corpo: {0}", corpo);
        corpo.GetProperty("type").GetString().Should().EndWith("/transicao-invalida");
        corpo.GetProperty("detail").GetString().Should().Contain(explicacao);
        (await DetalheAsync(token, equipe.TenantId)).GetRawText().Should().Be(antes.GetRawText(), "nada muda");
        (await TrilhaAsync(equipe.TenantId)).Should().BeEmpty();
    }

    [Theory] // F14-69: a demonstração fica fora do ciclo (D1 a1)
    [InlineData("ativacao")]
    [InlineData("prorrogacao")]
    [InlineData("cancelamento")]
    [InlineData("reativacao")]
    public async Task Acao_EmpresaDeDemonstracao_422ENadaMuda(string acao)
    {
        var equipe = await Equipe.CriarAsync(api);
        await GravarSituacaoAsync(equipe.TenantId, "ATIVA", null, null);
        await using (var db = api.DaEquipe(equipe.TenantId))
            await db.Database.ExecuteSqlAsync($"UPDATE tenants SET demonstracao = TRUE WHERE id = {equipe.TenantId}");
        var token = await OperadorNebrys.TokenAsync(api);
        var antes = await DetalheAsync(token, equipe.TenantId);
        var corpoDaAcao = acao == "prorrogacao" ? new { ate = Dia(Hoje().AddDays(10)) } : null;

        var (status, corpo) = await AgirAsync(token, equipe.TenantId, acao, null, corpoDaAcao);

        status.Should().Be((HttpStatusCode)422, "corpo: {0}", corpo);
        corpo.GetProperty("type").GetString().Should().EndWith("/transicao-invalida");
        corpo.GetProperty("detail").GetString().Should().Contain("demonstração");
        (await DetalheAsync(token, equipe.TenantId)).GetRawText().Should().Be(antes.GetRawText());
    }

    // ---------- F14-106: empresa expurgada ----------

    [Theory] // F14-106
    [InlineData("ativacao")]
    [InlineData("prorrogacao")]
    [InlineData("cancelamento")]
    [InlineData("reativacao")]
    public async Task Acao_EmpresaExpurgadaOuInexistente_404(string acao)
    {
        var token = await OperadorNebrys.TokenAsync(api);
        var corpoDaAcao = acao == "prorrogacao" ? new { ate = Dia(Hoje().AddDays(10)) } : null;
        var expurgada = Guid.NewGuid();

        var (status, corpo) = await AgirAsync(token, expurgada, acao, null, corpoDaAcao);

        status.Should().Be(HttpStatusCode.NotFound, "corpo: {0}", corpo);
        corpo.GetProperty("type").GetString().Should().EndWith("/nao-encontrado");
        (await TrilhaAsync(expurgada)).Should().BeEmpty();
    }

    [Fact] // F14-106: o diário expurgou a empresa cancelada, e a área não a acha mais
    public async Task Acao_DepoisDoExpurgo_404AoAbrirEAoAgir()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cancelamento = api.Relogio.Agora - PrazoAteExpurgo - TimeSpan.FromDays(1);
        await GravarSituacaoAsync(equipe.TenantId, "CANCELADA", cancelamento, cancelamento);
        await DiarioAsync();
        var token = await OperadorNebrys.TokenAsync(api);

        using var detalhe = await OperadorNebrys.EnviarAsync(api.NovoHttp(), HttpMethod.Get, $"{Area}/empresas/{equipe.TenantId}", token);
        var (status, _) = await AgirAsync(token, equipe.TenantId, "reativacao", null);

        detalhe.StatusCode.Should().Be(HttpStatusCode.NotFound);
        status.Should().Be(HttpStatusCode.NotFound);
        (await TrilhaAsync(equipe.TenantId)).Should().BeEmpty();
    }

    // ---------- F14-107: versão divergente ----------

    [Fact]
    public async Task Acao_EmpresaMudouDepoisDaLeitura_409ENadaMuda() // F14-107
    {
        var equipe = await Equipe.CriarAsync(api);
        var token = await OperadorNebrys.TokenAsync(api);
        var lida = await VersaoAsync(token, equipe.TenantId);
        // O diário venceu a empresa depois da leitura.
        await GravarSituacaoAsync(equipe.TenantId, "VENCIDA", api.Relogio.Agora, null);
        var antes = await DetalheAsync(token, equipe.TenantId);

        var (status, corpo) = await AgirAsync(
            token, equipe.TenantId, "prorrogacao", lida, new { ate = Dia(Hoje().AddDays(10)) });

        status.Should().Be(HttpStatusCode.Conflict, "corpo: {0}", corpo);
        corpo.GetProperty("type").GetString().Should().EndWith("/conflito-versao");
        (await DetalheAsync(token, equipe.TenantId)).GetRawText().Should().Be(antes.GetRawText(), "nada muda");
        (await TrilhaAsync(equipe.TenantId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Acao_OutraAcaoMudouAEmpresa_409MesmoQuandoATransicaoNaoValeriaMais() // F14-107
    {
        var equipe = await Equipe.CriarAsync(api);
        var token = await OperadorNebrys.TokenAsync(api);
        var lida = await VersaoAsync(token, equipe.TenantId);
        (await AgirAsync(token, equipe.TenantId, "cancelamento", lida)).Status.Should().Be(HttpStatusCode.OK);

        // A segunda ação com a versão velha: a empresa já está cancelada, mas a resposta é a da versão.
        var (status, corpo) = await AgirAsync(token, equipe.TenantId, "cancelamento", lida);

        status.Should().Be(HttpStatusCode.Conflict, "corpo: {0}", corpo);
        corpo.GetProperty("type").GetString().Should().EndWith("/conflito-versao");
        (await TrilhaAsync(equipe.TenantId)).Should().ContainSingle();
    }

    [Fact]
    public async Task Acao_IfMatchMalformado_400()
    {
        var equipe = await Equipe.CriarAsync(api);
        var token = await OperadorNebrys.TokenAsync(api);

        var (status, _) = await AgirAsync(token, equipe.TenantId, "ativacao", "versao-qualquer");

        status.Should().Be(HttpStatusCode.BadRequest);
        (await DetalheAsync(token, equipe.TenantId)).GetProperty("situacao").GetString().Should().Be("TESTE");
    }

    // ---------- Trilha das ações (D14; F14-70 confere tudo no F14p) ----------

    [Fact]
    public async Task Acao_Feita_TrilhaSemTenantComOOperadorAEmpresaEOAntesEDepois()
    {
        var equipe = await Equipe.CriarAsync(api);
        var token = await OperadorNebrys.TokenAsync(api);
        var operador = await OperadorNebrys.ContaAsync(api.App, OperadorNebrys.Email);

        (await AgirAsync(token, equipe.TenantId, "cancelamento", null)).Status.Should().Be(HttpStatusCode.OK);

        var trilha = await TrilhaAsync(equipe.TenantId);
        trilha.Should().ContainSingle();
        var (acao, usuario, alvoTipo, dados) = trilha[0];
        acao.Should().Be("nebrys.empresa.cancelamento");
        usuario.Should().Be(operador);
        alvoTipo.Should().Be("empresa");
        var json = JsonDocument.Parse(dados).RootElement;
        json.GetProperty("antes").GetProperty("situacao").GetString().Should().Be("TESTE");
        json.GetProperty("depois").GetProperty("situacao").GetString().Should().Be("CANCELADA");
        json.GetProperty("depois").GetProperty("canceladaEm").GetDateTimeOffset().Should().Be(api.Relogio.Agora);
        // Fora do alcance da empresa: a trilha dela não mostra a ação da Nebrys.
        var daEmpresa = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auditoria?acao=nebrys.empresa.cancelamento");
        daEmpresa.GetProperty("total").GetInt32().Should().Be(0);
    }

    [Fact] // F14-63 vale para as rotas novas
    public async Task Acoes_SessaoDeEmpresa_404SemMudarNada()
    {
        var equipe = await Equipe.CriarAsync(api);

        foreach (var acao in new[] { "ativacao", "prorrogacao", "cancelamento", "reativacao" })
        {
            using var resposta = await equipe.EnviarAsync(HttpMethod.Post, $"{Area}/empresas/{equipe.TenantId}/{acao}", new { });
            resposta.StatusCode.Should().Be(HttpStatusCode.NotFound, acao);
        }

        using var liberacao = await equipe.EnviarAsync(HttpMethod.Post, $"{Area}/documentos/liberacao", new { documento = "52998224725" });
        liberacao.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SituacaoNoEuAsync(equipe)).Should().Be("TESTE");
    }

    [Fact] // F14-66 (borda): "até hoje" vale até o fim do dia, no fuso da empresa
    public async Task Prorrogar_AteHoje_FimNoInicioDeAmanha()
    {
        var equipe = await Equipe.CriarAsync(api);
        var token = await OperadorNebrys.TokenAsync(api);

        var (status, corpo) = await AgirAsync(token, equipe.TenantId, "prorrogacao", null, new { ate = Dia(Hoje()) });

        status.Should().Be(HttpStatusCode.OK, "corpo: {0}", corpo);
        corpo.GetProperty("situacao").GetString().Should().Be("TESTE");
        corpo.GetProperty("fimDoTeste").GetDateTimeOffset().Should().Be(InicioDoDia(Hoje().AddDays(1)));
    }

    [Theory] // D14: cada ação da situação deixa a sua linha na trilha sem tenant, com o antes e o depois
    [InlineData("ativacao", "nebrys.empresa.ativacao", "TESTE", "ATIVA")]
    [InlineData("prorrogacao", "nebrys.empresa.prorrogacao", "VENCIDA", "TESTE")]
    [InlineData("reativacao", "nebrys.empresa.reativacao", "CANCELADA", "ATIVA")]
    public async Task Acao_Feita_TrilhaComANomeDaAcaoEOAntesEDepois(string acao, string nomeNaTrilha, string antes, string depois)
    {
        var equipe = await Equipe.CriarAsync(api);
        var desde = antes == "TESTE" ? (DateTimeOffset?)null : api.Relogio.Agora - TimeSpan.FromDays(2);
        await GravarSituacaoAsync(equipe.TenantId, antes, desde, antes == "CANCELADA" ? desde : null);
        var token = await OperadorNebrys.TokenAsync(api);
        var operador = await OperadorNebrys.ContaAsync(api.App, OperadorNebrys.Email);
        var corpoDaAcao = acao == "prorrogacao" ? new { ate = Dia(Hoje().AddDays(5)) } : null;

        (await AgirAsync(token, equipe.TenantId, acao, null, corpoDaAcao)).Status.Should().Be(HttpStatusCode.OK);

        var trilha = await TrilhaAsync(equipe.TenantId);
        trilha.Should().ContainSingle();
        trilha[0].Acao.Should().Be(nomeNaTrilha);
        trilha[0].Usuario.Should().Be(operador);
        var json = JsonDocument.Parse(trilha[0].Dados).RootElement;
        json.GetProperty("antes").GetProperty("situacao").GetString().Should().Be(antes);
        json.GetProperty("depois").GetProperty("situacao").GetString().Should().Be(depois);
    }

    [Fact] // ADR 0014, item 1: operador tirado da lista, o token antigo recebe 404 e a ação não acontece
    public async Task Acao_OperadorTiradoDaLista_404ENadaMuda()
    {
        var equipe = await Equipe.CriarAsync(api);
        var token = await OperadorNebrys.TokenAsync(api);
        await using var outraLista = await api.NovaApiAsync(
            new Dictionary<string, string?> { ["Nebrys:Operadores"] = "outra-pessoa@nebrys.test" });

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri($"{Area}/empresas/{equipe.TenantId}/cancelamento", UriKind.Relative));
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resposta = await outraLista.GetTestClient().SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SituacaoNoEuAsync(equipe)).Should().Be("TESTE");
        (await TrilhaAsync(equipe.TenantId)).Should().BeEmpty();
    }

    [Fact] // F14-63 vale para a liberação: sem token, nada é liberado
    public async Task Liberar_SemSessao_404ENadaMuda()
    {
        var cpf = DocumentosDeTeste.NovoCpf();
        (await RegistrarAsync(cpf)).Should().Be(HttpStatusCode.Created);
        using var http = api.NovoHttp();

        using var resposta = await http.PostAsJsonAsync(new Uri($"{Area}/documentos/liberacao", UriKind.Relative), new { documento = cpf });

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await RegistrarAsync(cpf)).Should().Be(HttpStatusCode.BadRequest, "nada foi liberado");
    }

    // ---------- F14-71 e F14-108: liberação de documento ----------

    [Fact]
    public async Task Liberar_DocumentoQueJaTeveTeste_NovoCadastroPassaEALiberacaoFicaNaTrilha() // F14-71
    {
        var cpf = DocumentosDeTeste.NovoCpf();
        (await RegistrarAsync(cpf)).Should().Be(HttpStatusCode.Created);
        (await RegistrarAsync(cpf)).Should().Be(HttpStatusCode.BadRequest, "o documento já teve teste (F14-57)");
        var token = await OperadorNebrys.TokenAsync(api);
        var operador = await OperadorNebrys.ContaAsync(api.App, OperadorNebrys.Email);

        var (status, corpo) = await LiberarAsync(token, DocumentosDeTeste.ComPontuacaoCpf(cpf));

        status.Should().Be(HttpStatusCode.NoContent, "corpo: {0}", corpo);
        (await RegistrarAsync(cpf)).Should().Be(HttpStatusCode.Created, "a liberação vale (F14-59)");
        (await RegistrarAsync(cpf)).Should().Be(HttpStatusCode.BadRequest, "para um cadastro só");
        var trilha = await TrilhaDaLiberacaoAsync(api.Relogio.Agora);
        trilha.Should().Contain(t => t.Usuario == operador);
        trilha.Should().AllSatisfy(t => t.Dados.Should().NotContain(cpf).And.NotContain(DocumentosDeTeste.Hash(cpf)));
    }

    [Fact]
    public async Task Liberar_DocumentoDeEmpresaExpurgada_LiberaPeloRegistroSemGuardarODocumento() // F14-108
    {
        // Da empresa expurgada só ficou o registro (o HMAC), sem empresa nem pessoa (F14-37).
        var cnpj = DocumentosDeTeste.Novo();
        var hash = DocumentosDeTeste.Hash(cnpj);
        await using (var db = api.DaEquipe(Guid.NewGuid()))
        {
            db.Add(DocumentoTeste.Registrar(hash, api.Relogio.Agora - TimeSpan.FromDays(90)));
            await db.SaveChangesAsync();
        }

        (await RegistrarAsync(cnpj)).Should().Be(HttpStatusCode.BadRequest);
        var token = await OperadorNebrys.TokenAsync(api);

        var (status, corpo) = await LiberarAsync(token, DocumentosDeTeste.ComPontuacaoCnpj(cnpj));

        status.Should().Be(HttpStatusCode.NoContent, "corpo: {0}", corpo);
        await using (var db = api.DaEquipe(Guid.NewGuid()))
        {
            var registro = await db.Set<DocumentoTeste>().AsNoTracking().SingleAsync(d => d.Hash == hash);
            registro.LiberadoEm.Should().Be(api.Relogio.Agora);
            registro.LiberacaoDisponivel.Should().BeTrue();
            (await db.Set<DocumentoTeste>().CountAsync(d => d.Hash == cnpj)).Should().Be(0, "o documento em claro não vira registro");
        }

        (await TrilhaDaLiberacaoAsync(api.Relogio.Agora))
            .Should().AllSatisfy(t => t.Dados.Should().NotContain(cnpj).And.NotContain(hash));
        (await RegistrarAsync(cnpj)).Should().Be(HttpStatusCode.Created, "a liberação vale como no F14-71");
    }

    [Fact]
    public async Task Liberar_DocumentoSemTeste_422SemCriarRegistro() // F14-71
    {
        var cpf = DocumentosDeTeste.NovoCpf();
        var token = await OperadorNebrys.TokenAsync(api);

        var (status, corpo) = await LiberarAsync(token, cpf);

        status.Should().Be((HttpStatusCode)422, "corpo: {0}", corpo);
        corpo.GetProperty("type").GetString().Should().EndWith("/transicao-invalida");
        await using var db = api.DaEquipe(Guid.NewGuid());
        (await db.Set<DocumentoTeste>().AnyAsync(d => d.Hash == DocumentosDeTeste.Hash(cpf))).Should().BeFalse();
    }

    [Fact]
    public async Task Liberar_LiberacaoAindaNaoUsada_422() // F14-71
    {
        var cpf = DocumentosDeTeste.NovoCpf();
        (await RegistrarAsync(cpf)).Should().Be(HttpStatusCode.Created);
        var token = await OperadorNebrys.TokenAsync(api);
        (await LiberarAsync(token, cpf)).Status.Should().Be(HttpStatusCode.NoContent);

        var (status, corpo) = await LiberarAsync(token, cpf);

        status.Should().Be((HttpStatusCode)422, "corpo: {0}", corpo);
        corpo.GetProperty("detail").GetString().Should().Contain("ainda não usada");
    }

    [Theory]
    [InlineData("")]
    [InlineData("529.982.247-26")]
    public async Task Liberar_DocumentoVazioOuInvalido_400NoCampo(string documento)
    {
        var token = await OperadorNebrys.TokenAsync(api);

        var (status, corpo) = await LiberarAsync(token, documento);

        status.Should().Be(HttpStatusCode.BadRequest, "corpo: {0}", corpo);
        corpo.GetProperty("errors").TryGetProperty("documento", out _).Should().BeTrue();
    }

    // ---------- apoio ----------

    private DateOnly Hoje() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(api.Relogio.Agora, SaoPaulo).DateTime);

    private static string Dia(DateOnly dia) => dia.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static DateTimeOffset InicioDoDia(DateOnly dia) =>
        new(TimeZoneInfo.ConvertTimeToUtc(dia.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), SaoPaulo), TimeSpan.Zero);

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> AgirAsync(
        string token, Guid empresa, string acao, string? versao, object? corpo = null)
    {
        using var http = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri($"{Area}/empresas/{empresa}/{acao}", UriKind.Relative));
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (versao is not null)
            requisicao.Headers.TryAddWithoutValidation("If-Match", $"\"{versao}\"");
        if (corpo is not null)
            requisicao.Content = JsonContent.Create(corpo);
        using var resposta = await http.SendAsync(requisicao);
        return (resposta.StatusCode, await CorpoAsync(resposta));
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> LiberarAsync(string token, string documento)
    {
        using var resposta = await OperadorNebrys.EnviarAsync(
            api.NovoHttp(), HttpMethod.Post, $"{Area}/documentos/liberacao", token,
            new StringContent(JsonSerializer.Serialize(new { documento }), Encoding.UTF8, "application/json"));
        return (resposta.StatusCode, await CorpoAsync(resposta));
    }

    private static async Task<JsonElement> CorpoAsync(HttpResponseMessage resposta)
    {
        var texto = await resposta.Content.ReadAsStringAsync();
        return texto.Length == 0 ? default : JsonDocument.Parse(texto).RootElement.Clone();
    }

    private async Task DiarioAsync()
    {
        using var http = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/internal/jobs/diario", UriKind.Relative));
        requisicao.Headers.Add(JobsEndpoints.CabecalhoToken, ApiFixture.TokenJobs);
        using var resposta = await http.SendAsync(requisicao);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
    }

    private async Task<JsonElement> DetalheAsync(string token, Guid empresa)
    {
        using var resposta = await OperadorNebrys.EnviarAsync(api.NovoHttp(), HttpMethod.Get, $"{Area}/empresas/{empresa}", token);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await CorpoAsync(resposta);
    }

    private async Task<string> VersaoAsync(string token, Guid empresa) =>
        (await DetalheAsync(token, empresa)).GetProperty("versao").GetString()!;

    private static async Task<string> SituacaoNoEuAsync(Equipe equipe) =>
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu")).GetProperty("tenant").GetProperty("situacao").GetString()!;

    /// <summary>A equipe tenta uma gravação da empresa e recebe a recusa de só leitura (F14-14).</summary>
    private static async Task EsperarRecusaDeSoLeituraAsync(Equipe equipe)
    {
        var recusa = await equipe.EsperarAsync((HttpStatusCode)422, HttpMethod.Post, "/api/v1/clientes", new { nome = "Cliente recusado" });
        recusa.GetProperty("type").GetString().Should().EndWith("/empresa-somente-leitura");
    }

    private async Task<HttpStatusCode> RegistrarAsync(string documento)
    {
        using var http = api.NovoHttp();
        using var resposta = await http.PostAsJsonAsync(new Uri("/api/v1/auth/registro", UriKind.Relative), new
        {
            nome = "Ana Souza", email = $"liberacao-{Guid.NewGuid():N}@exemplo.com", senha = Equipe.Senha, nomeEmpresa = "Estúdio Traço",
            documento, versaoTermos = TextosAceitos.Termos, versaoPrivacidade = TextosAceitos.Privacidade,
        });
        return resposta.StatusCode;
    }

    private async Task GravarSituacaoAsync(Guid tenantId, string situacao, DateTimeOffset? somenteLeituraDesde, DateTimeOffset? canceladaEm)
    {
        await using var db = api.DaEquipe(tenantId);
        await db.Database.ExecuteSqlAsync($"""
            UPDATE tenants SET situacao = {situacao}, somente_leitura_desde = {somenteLeituraDesde}, cancelada_em = {canceladaEm}
            WHERE id = {tenantId}
            """);
    }

    private async Task<DateTimeOffset?> GravavelDesdeAsync(Guid tenantId)
    {
        await using var db = api.DaEquipe(tenantId);
        return (await db.Tenants.AsNoTracking().SingleAsync()).GravavelDesde;
    }

    /// <summary>As ações da área sobre a empresa, na trilha sem tenant (lidas fora dos filtros).</summary>
    private async Task<List<(string Acao, Guid? Usuario, string? AlvoTipo, string Dados)>> TrilhaAsync(Guid empresa)
    {
        await using var db = api.DaEquipe(Guid.NewGuid());
        var linhas = await db.Database.SqlQuery<LinhaTrilha>($"""
            SELECT acao, usuario_id, alvo_tipo, dados::text AS dados FROM auditoria
            WHERE tenant_id IS NULL AND alvo_id = {empresa} AND acao LIKE 'nebrys.%'
            ORDER BY ocorrido_em
            """).ToListAsync();
        return linhas.Select(l => (l.Acao, l.UsuarioId, l.AlvoTipo, l.Dados ?? "")).ToList();
    }

    /// <summary>As liberações gravadas no instante dado, sem tenant e sem alvo.</summary>
    private async Task<List<(Guid? Usuario, string Dados)>> TrilhaDaLiberacaoAsync(DateTimeOffset instante)
    {
        await using var db = api.DaEquipe(Guid.NewGuid());
        var linhas = await db.Database.SqlQuery<LinhaTrilha>($"""
            SELECT acao, usuario_id, alvo_tipo, dados::text AS dados FROM auditoria
            WHERE tenant_id IS NULL AND alvo_id IS NULL AND acao = 'nebrys.documento.liberacao' AND ocorrido_em = {instante}
            """).ToListAsync();
        linhas.Should().NotBeEmpty();
        linhas.Should().AllSatisfy(l => l.AlvoTipo.Should().Be("documento"));
        return linhas.Select(l => (l.UsuarioId, l.Dados ?? "")).ToList();
    }

    private sealed class LinhaTrilha
    {
        // Colunas pelo nome em snake_case (a convenção do contexto vale também para o tipo não mapeado).
        public string Acao { get; set; } = "";

        public Guid? UsuarioId { get; set; }

        public string? AlvoTipo { get; set; }

        public string? Dados { get; set; }
    }
}
