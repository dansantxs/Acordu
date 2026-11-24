using System.Net;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Jobs;
using Acordu.Application.Exemplos;
using Acordu.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using static Acordu.Api.FunctionalTests.Apoio.Cenarios;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F14, F14-91 a F14-96 (RF-EMP-05; design D13; contrato §6): a empresa em teste sem dados carrega os dados de
/// exemplo sem enviar e-mail, só o Dono carrega e apaga, apagar leva o que a equipe criou dentro dos exemplos e deixa o
/// resto, e os exemplos de uma empresa nunca aparecem noutra. Carregar e apagar são gravação da empresa (ADR 0015).
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class ExemplosTests(ApiFixture api)
{
    private const string Rota = "/api/v1/configuracoes/exemplos";
    private const string Erros = "https://acordu.app.br/erros/";

    private static async Task<JsonElement> EstadoAsync(Equipe equipe) =>
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, Rota);

    private static Task CarregarAsync(Equipe equipe) => equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Post, Rota);

    private static async Task<int> TotalAsync(Equipe equipe, string rota) =>
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, rota)).GetProperty("total").GetInt32();

    private static async Task<List<JsonElement>> ClientesAsync(Equipe equipe) =>
        [.. (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/clientes?tamanhoPagina=50")).GetProperty("itens").EnumerateArray()];

    private static async Task<Guid> SolicitacaoDoExemploAsync(Equipe equipe, string titulo)
    {
        var lista = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes?busca={Uri.EscapeDataString(titulo)}");
        return Id(lista.GetProperty("itens").EnumerateArray().Single(s => s.GetProperty("titulo").GetString() == titulo));
    }

    private async Task<List<Guid>> MensagensNaOutboxAsync(Guid tenantId)
    {
        await using var db = api.DaEquipe(tenantId);
        return await db.Set<MensagemOutbox>().IgnoreQueryFilters().Where(m => m.TenantId == tenantId).Select(m => m.Id).ToListAsync();
    }

    /// <summary>Põe a empresa na situação, direto no banco (como os testes do F14b).</summary>
    private async Task SituacaoAsync(Guid tenantId, string situacao)
    {
        var desde = api.Relogio.Agora.ToUniversalTime();
        await using var db = api.DaEquipe(tenantId);
        await db.Database.ExecuteSqlAsync(
            $"UPDATE tenants SET situacao = {situacao}, somente_leitura_desde = {desde} WHERE id = {tenantId}");
    }

    [Fact]
    public async Task Obter_EmpresaEmTesteSemDados_OfereceCarregar() // F14-91
    {
        var equipe = await Equipe.CriarAsync(api);

        var estado = await EstadoAsync(equipe);

        estado.GetProperty("disponivel").GetBoolean().Should().BeTrue();
        estado.GetProperty("carregados").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Carregar_EmpresaVazia_CriaOsExemplosMarcadosSemEnviarEmail() // F14-92
    {
        var equipe = await Equipe.CriarAsync(api);
        var outboxAntes = await MensagensNaOutboxAsync(equipe.TenantId);

        await CarregarAsync(equipe);

        var dados = DadosFicticios.Arquivo;
        var clientes = await ClientesAsync(equipe);
        clientes.Should().HaveCount(dados.Clientes.Count)
            .And.OnlyContain(c => c.GetProperty("exemplo").GetBoolean(), "os clientes vêm marcados como exemplo");
        var padaria = clientes.Single(c => c.GetProperty("nome").GetString() == dados.Clientes[0].Nome);
        var detalhe = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/clientes/{Id(padaria)}");
        detalhe.GetProperty("exemplo").GetBoolean().Should().BeTrue();
        detalhe.GetProperty("contatos").GetArrayLength().Should().Be(1);
        (await TotalAsync(equipe, "/api/v1/contratos")).Should().Be(dados.Clientes.Count);
        (await TotalAsync(equipe, "/api/v1/solicitacoes?tamanhoPagina=50")).Should().BePositive();
        (await TotalAsync(equipe, "/api/v1/tarefas?tamanhoPagina=200")).Should().BePositive();
        (await TotalAsync(equipe, $"/api/v1/apontamentos?clienteId={Id(padaria)}")).Should().BePositive();

        (await MensagensNaOutboxAsync(equipe.TenantId)).Except(outboxAntes).Should().BeEmpty("a carga não passa pela outbox");
        dados.Clientes.SelectMany(c => api.Emails.Para(c.Contato.Email)).Should().BeEmpty("nenhum e-mail sai (RF-EMP-05)");
        api.Entregas.Entregues.Should().NotContain(e => e.TenantId == equipe.TenantId);
        (await TotalAsync(equipe, "/api/v1/auditoria?acao=exemplos.carga")).Should().Be(1);

        var estado = await EstadoAsync(equipe);
        estado.GetProperty("disponivel").GetBoolean().Should().BeFalse("os exemplos não são carregados duas vezes");
        estado.GetProperty("carregados").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Carregar_EmpresaComDadosProprios_NaoOfereceERecusa422SemCarregar() // F14-93
    {
        var equipe = await Equipe.CriarAsync(api);
        await ClienteAsync(equipe);

        var estado = await EstadoAsync(equipe);
        var problema = await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Post, Rota);

        estado.GetProperty("disponivel").GetBoolean().Should().BeFalse();
        problema.GetProperty("type").GetString().Should().Be(Erros + "transicao-invalida");
        problema.GetProperty("detail").GetString().Should().Be(ComandosExemplos.MensagemIndisponivel);
        (await ClientesAsync(equipe)).Should().ContainSingle().Which.GetProperty("exemplo").GetBoolean().Should().BeFalse();
        (await TotalAsync(equipe, "/api/v1/auditoria?acao=exemplos.carga")).Should().Be(0);
    }

    [Fact]
    public async Task Carregar_ExemplosJaCarregados_ASegundaRecusa()
    {
        var equipe = await Equipe.CriarAsync(api);
        await CarregarAsync(equipe);

        await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Post, Rota);

        (await ClientesAsync(equipe)).Should().HaveCount(DadosFicticios.Arquivo.Clientes.Count);
    }

    [Fact]
    public async Task Carregar_EmpresaAtiva_NaoOferece()
    {
        var equipe = await Equipe.CriarAsync(api);
        await using (var db = api.DaEquipe(equipe.TenantId))
            await db.Database.ExecuteSqlAsync($"UPDATE tenants SET situacao = 'ATIVA', fim_do_teste = NULL WHERE id = {equipe.TenantId}");

        (await EstadoAsync(equipe)).GetProperty("disponivel").GetBoolean().Should().BeFalse();
        await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Post, Rota);
    }

    [Fact]
    public async Task Apagar_ComExemplos_LevaOQueAEquipeCriouDentroEDeixaOResto() // F14-94
    {
        var equipe = await Equipe.CriarAsync(api);
        await CarregarAsync(equipe);
        // O que a equipe criou dentro de um exemplo: uma tarefa na solicitação e um contato no cliente.
        var lentidao = await SolicitacaoDoExemploAsync(equipe, "Lentidão ao abrir a pasta compartilhada");
        var tarefaNoExemplo = await TarefaAsync(equipe, lentidao, "Medir a rede da advocacia");
        var advocacia = Id((await ClientesAsync(equipe)).Single(c => c.GetProperty("nome").GetString() == "Lima & Prado Advocacia"));
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/clientes/{advocacia}/contatos", new
        {
            nome = "Beatriz Lima", email = $"beatriz-{Guid.NewGuid():N}@exemplo.invalid",
        });
        // O que é da empresa: cliente, solicitação e tarefa interna.
        var real = await ClienteAsync(equipe, "Floricultura Primavera");
        var solicitacaoReal = await SolicitacaoAsync(equipe, real);
        var tarefaInterna = await TarefaAsync(equipe, titulo: "Organizar o arquivo");

        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, Rota);

        (await ClientesAsync(equipe)).Select(Id).Should().Equal(real);
        var solicitacoes = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/solicitacoes?tamanhoPagina=50");
        solicitacoes.GetProperty("itens").EnumerateArray().Select(Id).Should().Equal(solicitacaoReal);
        await equipe.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"/api/v1/tarefas/{tarefaNoExemplo}");
        await equipe.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"/api/v1/solicitacoes/{lentidao}");
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/tarefas/{tarefaInterna}");
        (await TotalAsync(equipe, "/api/v1/contratos")).Should().Be(0);
        (await TotalAsync(equipe, "/api/v1/auditoria?acao=exemplos.remocao")).Should().Be(1);
        var estado = await EstadoAsync(equipe);
        estado.GetProperty("carregados").GetBoolean().Should().BeFalse();
        estado.GetProperty("disponivel").GetBoolean().Should().BeFalse("a empresa já tem dados próprios");
    }

    [Fact]
    public async Task Apagar_SoComExemplos_VoltaAOferecer()
    {
        var equipe = await Equipe.CriarAsync(api);
        await CarregarAsync(equipe);

        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, Rota);

        (await ClientesAsync(equipe)).Should().BeEmpty();
        (await TotalAsync(equipe, "/api/v1/tarefas?tamanhoPagina=200")).Should().Be(0);
        (await EstadoAsync(equipe)).GetProperty("disponivel").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Apagar_SemExemplos_NaoMudaNada()
    {
        var equipe = await Equipe.CriarAsync(api);
        var real = await ClienteAsync(equipe);

        await equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, Rota);

        (await ClientesAsync(equipe)).Select(Id).Should().Equal(real);
        (await TotalAsync(equipe, "/api/v1/auditoria?acao=exemplos.remocao")).Should().Be(0);
    }

    [Theory] // F14-95
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("DELETE")]
    public async Task Exemplos_SemConfiguracaoGerenciar_403(string metodo)
    {
        var equipe = await Equipe.CriarAsync(api);
        var membro = await equipe.ComPapelAsync("Membro");

        await equipe.EsperarAsync(HttpStatusCode.Forbidden, new HttpMethod(metodo), Rota, token: membro);

        (await ClientesAsync(equipe)).Should().BeEmpty("nada foi carregado");
    }

    [Theory] // F14-95
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("DELETE")]
    public async Task Exemplos_Contato_403(string metodo)
    {
        var equipe = await Equipe.CriarAsync(api);
        await CarregarAsync(equipe);
        var advocacia = Id((await ClientesAsync(equipe)).Single(c => c.GetProperty("nome").GetString() == "Lima & Prado Advocacia"));
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, advocacia);

        await equipe.EsperarAsync(HttpStatusCode.Forbidden, new HttpMethod(metodo), Rota, token: contato.Token);

        (await ClientesAsync(equipe)).Should().HaveCount(DadosFicticios.Arquivo.Clientes.Count, "nada foi apagado nem carregado de novo");
    }

    [Fact]
    public async Task Exemplos_DeUmaEmpresa_NaoAparecemNemSaemNaOutra() // F14-96
    {
        var a = await Equipe.CriarAsync(api);
        var b = await Equipe.CriarAsync(api);
        await CarregarAsync(a);
        var exemploDeA = Id((await ClientesAsync(a))[0]);
        var lentidaoDeA = await SolicitacaoDoExemploAsync(a, "Lentidão ao abrir a pasta compartilhada");

        (await ClientesAsync(b)).Should().BeEmpty();
        (await TotalAsync(b, "/api/v1/solicitacoes")).Should().Be(0);
        (await TotalAsync(b, "/api/v1/tarefas?tamanhoPagina=200")).Should().Be(0);
        (await TotalAsync(b, "/api/v1/contratos")).Should().Be(0);
        (await TotalAsync(b, "/api/v1/apontamentos")).Should().Be(0);
        await b.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"/api/v1/clientes/{exemploDeA}");
        await b.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"/api/v1/solicitacoes/{lentidaoDeA}");
        var estadoDeB = await EstadoAsync(b);
        estadoDeB.GetProperty("carregados").GetBoolean().Should().BeFalse();
        estadoDeB.GetProperty("disponivel").GetBoolean().Should().BeTrue("os exemplos de A não contam como dado de B");

        await b.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, Rota);

        (await ClientesAsync(a)).Should().HaveCount(DadosFicticios.Arquivo.Clientes.Count, "apagar em B não toca A");
        await a.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{lentidaoDeA}");
    }

    [Fact] // RF-EMP-05: os contatos dos exemplos são fictícios; nem o que a equipe faz depois manda e-mail a eles
    public async Task Interacao_ExternaNumExemplo_NaoAvisaOContato()
    {
        var equipe = await Equipe.CriarAsync(api);
        await CarregarAsync(equipe);
        var pdv = await SolicitacaoDoExemploAsync(equipe, "Caixa não imprime o cupom desde ontem");

        using (var resposta = await equipe.EnviarAsync(
            HttpMethod.Post, $"/api/v1/solicitacoes/{pdv}/interacoes", new { texto = "A peça nova chegou.", visibilidade = "EXTERNA" }))
            resposta.IsSuccessStatusCode.Should().BeTrue();
        await TickAsync();

        DadosFicticios.Arquivo.Clientes.SelectMany(c => api.Emails.Para(c.Contato.Email)).Should().BeEmpty();
    }

    [Fact] // RF-EMP-05: o SLA dos exemplos não gera alerta nem e-mail ao Dono
    public async Task AlertasSla_SolicitacoesDoExemplo_NaoAlertam()
    {
        var equipe = await Equipe.CriarAsync(api);
        await CarregarAsync(equipe);
        var inicio = api.Relogio.Agora;

        api.Relogio.Agora = inicio.AddDays(3);
        try
        {
            await TickAsync();
        }
        finally
        {
            api.Relogio.Agora = inicio;
        }

        api.Emails.Para(equipe.EmailDono).Should().NotContain(m => m.Categoria.StartsWith("aviso-sla", StringComparison.Ordinal));
    }

    [Fact] // RF-EMP-05: o fechamento automático das resolvidas dos exemplos não avisa o contato fictício
    public async Task FechamentoAutomatico_ResolvidasDoExemplo_FechamSemEmail()
    {
        var equipe = await Equipe.CriarAsync(api);
        await CarregarAsync(equipe);
        var inicio = api.Relogio.Agora;

        api.Relogio.Agora = inicio.AddDays(30);
        try
        {
            using var http = api.NovoHttp();
            using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/internal/jobs/fechamento-automatico", UriKind.Relative));
            requisicao.Headers.Add(JobsEndpoints.CabecalhoToken, ApiFixture.TokenJobs);
            using var resposta = await http.SendAsync(requisicao);
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            await TickAsync();
        }
        finally
        {
            api.Relogio.Agora = inicio;
        }

        DadosFicticios.Arquivo.Clientes.SelectMany(c => api.Emails.Para(c.Contato.Email)).Should().BeEmpty();
        api.Entregas.Entregues.Should().NotContain(e => e.TenantId == equipe.TenantId);
        (await TotalAsync(equipe, "/api/v1/solicitacoes?status=FECHADA")).Should().BePositive("o job fechou as resolvidas dos exemplos");
    }

    private async Task TickAsync()
    {
        using var http = api.NovoHttp();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/internal/jobs/tick", UriKind.Relative));
        requisicao.Headers.Add(JobsEndpoints.CabecalhoToken, ApiFixture.TokenJobs);
        using var resposta = await http.SendAsync(requisicao);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory] // ADR 0015: carregar e apagar só em empresa gravável
    [InlineData("VENCIDA")]
    [InlineData("CANCELADA")]
    public async Task Carregar_EmpresaSoLeitura_RecusaComoSoLeitura(string situacao)
    {
        var equipe = await Equipe.CriarAsync(api);
        await SituacaoAsync(equipe.TenantId, situacao);

        var problema = await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Post, Rota);

        problema.GetProperty("type").GetString().Should().Be(Erros + "empresa-somente-leitura");
        (await ClientesAsync(equipe)).Should().BeEmpty();
        (await EstadoAsync(equipe)).GetProperty("disponivel").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Apagar_EmpresaSoLeitura_RecusaSemApagar() // ADR 0015
    {
        var equipe = await Equipe.CriarAsync(api);
        await CarregarAsync(equipe);
        await SituacaoAsync(equipe.TenantId, "VENCIDA");

        var problema = await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Delete, Rota);

        problema.GetProperty("type").GetString().Should().Be(Erros + "empresa-somente-leitura");
        (await ClientesAsync(equipe)).Should().HaveCount(DadosFicticios.Arquivo.Clientes.Count);
        (await TotalAsync(equipe, "/api/v1/tarefas?tamanhoPagina=200")).Should().BePositive("nada saiu");
    }
}
