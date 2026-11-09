using System.Net;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Contrato §12: rotas do portal com sessão de contato. Escopo, isolamento entre clientes, nada interno nas respostas,
/// retomada pela resposta do cliente, aprovação/reprovação, extrato e revogação imediata.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class PortalTests(ApiFixture api)
{
    private const string Portal = "/api/v1/portal";

    [Fact]
    public async Task Eu_ContatoClienteEPrestador()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);

        var eu = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"{Portal}/eu", token: contato.Token);

        eu.GetProperty("contato").GetProperty("id").GetGuid().Should().Be(contato.Id);
        eu.GetProperty("contato").GetProperty("email").GetString().Should().Be(contato.Email);
        eu.GetProperty("cliente").GetProperty("id").GetGuid().Should().Be(cliente);
        eu.GetProperty("cliente").GetProperty("nome").GetString().Should().Be("Padaria Bom Grão");
        eu.GetProperty("prestador").GetProperty("nome").GetString().Should().Be("Estúdio Traço");
        eu.GetProperty("escopoPortal").GetString().Should().Be("TODAS_DO_CLIENTE");
        eu.GetProperty("veContratos").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Responder_ComContaEAnexo_SolicitacaoEncerrada_409SemGuardarArquivo() // F11-08
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);
        var solicitacao = await Cenarios.SolicitacaoAsync(equipe, cliente);
        var rota = $"{Portal}/solicitacoes/{solicitacao}";
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, rota, token: contato.Token))
            .GetProperty("encerrada").GetBoolean().Should().BeFalse();
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/cancelamento",
            new { motivo = "Cliente desistiu" });

        using var formulario = new MultipartFormDataContent();
        formulario.Add(new StringContent("Segue o print"), "texto");
        formulario.Add(new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2]), "anexos", "tela.png");
        using var http = api.NovoHttp();
        using var pedido = new HttpRequestMessage(HttpMethod.Post, new Uri($"{rota}/interacoes", UriKind.Relative)) { Content = formulario };
        pedido.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", contato.Token);
        using var resposta = await http.SendAsync(pedido);

        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        api.Arquivos.ComPrefixo($"{equipe.TenantId}/").Should().Be(0, "o arquivo nem chega ao armazenamento");
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, rota, token: contato.Token))
            .GetProperty("encerrada").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Abrir_OrigemPortal_ComOContatoLogadoEOContratoVigente()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contrato = await Cenarios.ContratoAsync(equipe, cliente);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);

        var aberta = await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"{Portal}/solicitacoes",
            new { titulo = "Site fora do ar", descricao = "Desde as 9h", prioridade = "ALTA" }, contato.Token);

        aberta.GetProperty("statusExterno").GetString().Should().Be("RECEBIDA");
        aberta.GetProperty("sla").GetProperty("situacao").GetString().Should().Be("NO_PRAZO");
        aberta.GetProperty("podeAprovar").GetBoolean().Should().BeFalse();
        var interna = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{Cenarios.Id(aberta)}");
        interna.GetProperty("origem").GetString().Should().Be("PORTAL");
        interna.GetProperty("contato").GetProperty("id").GetGuid().Should().Be(contato.Id);
        interna.GetProperty("contratoId").GetGuid().Should().Be(contrato);
        interna.GetProperty("prioridade").GetString().Should().Be("ALTA");
    }

    [Fact]
    public async Task Detalhe_SoOQueEhVisivelAoCliente()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        await Cenarios.ContratoAsync(equipe, cliente);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);
        var solicitacao = await Cenarios.SolicitacaoAsync(equipe, cliente);
        var visivel = await Cenarios.TarefaAsync(equipe, solicitacao, "Refazer arte");
        var interna = Cenarios.Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/tarefas", new
        {
            titulo = "Conferir margem de lucro", solicitacaoId = solicitacao, visivelCliente = false,
        }));
        await Apontar(equipe, visivel, 90, visivelCliente: true);
        await Apontar(equipe, visivel, 30, visivelCliente: false);
        await Apontar(equipe, interna, 45, visivelCliente: false);
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/interacoes",
            new { texto = "Cliente difícil, cobrar adiantado", visibilidade = "INTERNA" });
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/interacoes",
            new { texto = "Estamos refazendo a arte", visibilidade = "EXTERNA" });

        using var resposta = await equipe.EnviarAsync(HttpMethod.Get, $"{Portal}/solicitacoes/{solicitacao}", token: contato.Token);
        using var respostaInteracoes = await equipe.EnviarAsync(
            HttpMethod.Get, $"{Portal}/solicitacoes/{solicitacao}/interacoes", token: contato.Token);
        var texto = await resposta.Content.ReadAsStringAsync();
        var textoInteracoes = await respostaInteracoes.Content.ReadAsStringAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var detalhe = JsonDocument.Parse(texto).RootElement;
        detalhe.GetProperty("tempoVisivelMinutos").GetInt32().Should().Be(90);
        detalhe.GetProperty("tarefasVisiveis").GetArrayLength().Should().Be(1);
        detalhe.GetProperty("tarefasVisiveis")[0].GetProperty("titulo").GetString().Should().Be("Refazer arte");
        texto.Should().NotContain("Conferir margem");
        foreach (var interno in new[] { "status", "versao", "prioridade", "contratoId", "origem", "solucao" })
            detalhe.TryGetProperty(interno, out _).Should().BeFalse(interno);
        var interacoes = JsonDocument.Parse(textoInteracoes).RootElement;
        interacoes.GetArrayLength().Should().Be(1);
        interacoes[0].GetProperty("texto").GetString().Should().Be("Estamos refazendo a arte");
        interacoes[0].GetProperty("daEquipe").GetBoolean().Should().BeTrue();
        interacoes[0].GetProperty("autorNome").GetString().Should().Be("Pessoa da equipe");
        textoInteracoes.Should().NotContain("cobrar adiantado").And.NotContain("autorId");
    }

    [Fact]
    public async Task EscopoProprias_SoAsDele_ESemContratos()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contrato = await Cenarios.ContratoAsync(equipe, cliente);
        var marina = await ContatosDoPortal.CriarAsync(api, equipe, cliente, "PROPRIAS");
        var joao = await ContatosDoPortal.CriarAsync(api, equipe, cliente, "TODAS_DO_CLIENTE", "João Lima");
        var doJoao = Cenarios.Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/solicitacoes", new
        {
            clienteId = cliente, contatoId = joao.Id, titulo = "Pedido do João", prioridade = "MEDIA",
        }));
        var semContato = await Cenarios.SolicitacaoAsync(equipe, cliente);
        var daMarina = Cenarios.Id(await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"{Portal}/solicitacoes", new { titulo = "Pedido da Marina" }, marina.Token));

        var listaMarina = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"{Portal}/solicitacoes", token: marina.Token);
        var listaJoao = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"{Portal}/solicitacoes", token: joao.Token);

        Ids(listaMarina).Should().Equal(daMarina);
        Ids(listaJoao).Should().BeEquivalentTo(new[] { daMarina, doJoao, semContato });
        await equipe.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"{Portal}/solicitacoes/{doJoao}", token: marina.Token);
        await equipe.EsperarAsync(
            HttpStatusCode.NotFound, HttpMethod.Post, $"{Portal}/solicitacoes/{doJoao}/interacoes", new { texto = "Oi" }, marina.Token);
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Get, $"{Portal}/contratos", token: marina.Token);
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Get, $"{Portal}/contratos/{contrato}/extrato", token: marina.Token);
        var eu = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"{Portal}/eu", token: marina.Token);
        eu.GetProperty("veContratos").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task OutroCliente_404_EEquipeNoPortal_403()
    {
        var equipe = await Equipe.CriarAsync(api);
        var padaria = await Cenarios.ClienteAsync(equipe);
        var oficina = await Cenarios.ClienteAsync(equipe, "Oficina do Zé");
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, padaria);
        var daOficina = await Cenarios.SolicitacaoAsync(equipe, oficina);
        var contratoOficina = await Cenarios.ContratoAsync(equipe, oficina);

        await equipe.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"{Portal}/solicitacoes/{daOficina}", token: contato.Token);
        await equipe.EsperarAsync(
            HttpStatusCode.NotFound, HttpMethod.Get, $"{Portal}/solicitacoes/{daOficina}/interacoes", token: contato.Token);
        await equipe.EsperarAsync(
            HttpStatusCode.NotFound, HttpMethod.Post, $"{Portal}/solicitacoes/{daOficina}/aprovacao", token: contato.Token);
        await equipe.EsperarAsync(
            HttpStatusCode.NotFound, HttpMethod.Get, $"{Portal}/contratos/{contratoOficina}/extrato", token: contato.Token);
        var lista = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"{Portal}/solicitacoes", token: contato.Token);
        lista.GetProperty("total").GetInt32().Should().Be(0);
        await equipe.EsperarAsync(HttpStatusCode.Forbidden, HttpMethod.Get, $"{Portal}/solicitacoes");
    }

    [Fact]
    public async Task RespostaDoCliente_RetomaASolicitacaoQueAguardavaEle()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        await Cenarios.ContratoAsync(equipe, cliente);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);
        var solicitacao = await Cenarios.SolicitacaoAsync(equipe, cliente);
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/aguardando-cliente");
        var aguardando = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Get, $"{Portal}/solicitacoes/{solicitacao}", token: contato.Token);
        aguardando.GetProperty("statusExterno").GetString().Should().Be("AGUARDANDO_VOCE");
        aguardando.GetProperty("sla").GetProperty("pausado").GetBoolean().Should().BeTrue();

        var resposta = await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"{Portal}/solicitacoes/{solicitacao}/interacoes",
            new { texto = "Segue o logo em alta" }, contato.Token);

        resposta.GetProperty("daEquipe").GetBoolean().Should().BeFalse();
        resposta.GetProperty("autorNome").GetString().Should().Be("Marina Souza");
        var interna = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}");
        interna.GetProperty("status").GetString().Should().Be("EM_ANDAMENTO");
        var interacoes = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}/interacoes");
        interacoes[0].GetProperty("autorEhCliente").GetBoolean().Should().BeTrue();
        interacoes[0].GetProperty("visibilidade").GetString().Should().Be("EXTERNA");
        var depois = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Get, $"{Portal}/solicitacoes/{solicitacao}", token: contato.Token);
        depois.GetProperty("statusExterno").GetString().Should().Be("EM_ANDAMENTO");
    }

    [Fact]
    public async Task ReprovarEAprovarAEntrega()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        await Cenarios.ContratoAsync(equipe, cliente);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);
        var solicitacao = await Cenarios.SolicitacaoAsync(equipe, cliente);
        var rota = $"{Portal}/solicitacoes/{solicitacao}";

        await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Post, $"{rota}/aprovacao", token: contato.Token);
        await Resolver(equipe, solicitacao);
        var resolvida = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, rota, token: contato.Token);
        // A entrega espera o contato (StatusNoPortal): "Esperando você", não "Concluída".
        resolvida.GetProperty("statusExterno").GetString().Should().Be("AGUARDANDO_VOCE");
        resolvida.GetProperty("podeAprovar").GetBoolean().Should().BeTrue();
        var esperando = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Get, $"{Portal}/solicitacoes?statusExterno=AGUARDANDO_VOCE", token: contato.Token);
        esperando.GetProperty("itens")[0].GetProperty("id").GetGuid().Should().Be(solicitacao);
        esperando.GetProperty("itens")[0].GetProperty("statusExterno").GetString().Should().Be("AGUARDANDO_VOCE");
        var concluidas = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Get, $"{Portal}/solicitacoes?statusExterno=CONCLUIDA", token: contato.Token);
        concluidas.GetProperty("total").GetInt32().Should().Be(0);

        var semMotivo = await equipe.EsperarAsync(
            HttpStatusCode.BadRequest, HttpMethod.Post, $"{rota}/reprovacao", new { motivo = " " }, contato.Token);
        semMotivo.GetProperty("errors").TryGetProperty("motivo", out _).Should().BeTrue();
        var reprovada = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Post, $"{rota}/reprovacao", new { motivo = "A cor do banner ficou errada" }, contato.Token);
        reprovada.GetProperty("statusExterno").GetString().Should().Be("EM_ANDAMENTO");
        reprovada.GetProperty("podeAprovar").GetBoolean().Should().BeFalse();

        await Resolver(equipe, solicitacao);
        var aprovada = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"{rota}/aprovacao", token: contato.Token);

        aprovada.GetProperty("statusExterno").GetString().Should().Be("CONCLUIDA");
        aprovada.GetProperty("podeAprovar").GetBoolean().Should().BeFalse();
        var interna = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}");
        interna.GetProperty("status").GetString().Should().Be("FECHADA");
        await equipe.EsperarAsync(HttpStatusCode.UnprocessableEntity, HttpMethod.Post, $"{rota}/aprovacao", token: contato.Token);
    }

    [Fact]
    public async Task ContratosEExtrato_TotalConferivelLinhaALinha()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contrato = await Cenarios.ContratoAsync(equipe, cliente);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);
        var solicitacao = await Cenarios.SolicitacaoAsync(equipe, cliente);
        var tarefa = await Cenarios.TarefaAsync(equipe, solicitacao);
        await Apontar(equipe, tarefa, 90, visivelCliente: true, descricaoExterna: "Arte refeita");
        await Apontar(equipe, tarefa, 30, visivelCliente: false);
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/apontamentos", new
        {
            tarefaId = tarefa, data = "2026-10-05", duracaoMinutos = 60, descricao = "Reunião interna", faturavel = false,
        });

        var contratos = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"{Portal}/contratos", token: contato.Token);
        var extrato = await equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Get, $"{Portal}/contratos/{contrato}/extrato?periodo=2026-10", token: contato.Token);

        contratos.GetArrayLength().Should().Be(1);
        contratos[0].GetProperty("id").GetGuid().Should().Be(contrato);
        contratos[0].GetProperty("tipo").GetString().Should().Be("BANCO_HORAS");
        extrato.GetProperty("contrato").GetProperty("franquiaMinutos").GetInt32().Should().Be(1200);
        extrato.GetProperty("periodo").GetString().Should().Be("2026-10");
        extrato.GetProperty("consumidoMinutos").GetInt32().Should().Be(120);
        extrato.GetProperty("saldoMinutos").GetInt32().Should().Be(1080);
        extrato.GetProperty("percentual").GetInt32().Should().Be(10);
        var itens = extrato.GetProperty("itens");
        itens.GetArrayLength().Should().Be(2);
        itens.EnumerateArray().Select(i => i.GetProperty("descricao").GetString())
            .Should().Contain("Arte refeita").And.Contain("Atendimento");
        itens.EnumerateArray().Sum(i => i.GetProperty("duracaoMinutos").GetInt32()).Should().Be(120);
    }

    [Fact]
    public async Task RevogarAcesso_ValeNaRequisicaoSeguinte()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"{Portal}/eu", token: contato.Token);

        await equipe.EsperarAsync(
            HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/v1/clientes/{cliente}/contatos/{contato.Id}/acesso-portal");

        var erro = await equipe.EsperarAsync(HttpStatusCode.Unauthorized, HttpMethod.Get, $"{Portal}/eu", token: contato.Token);
        erro.GetProperty("type").GetString().Should().EndWith("/nao-autenticado");
    }

    private static List<Guid> Ids(JsonElement pagina) =>
        [.. pagina.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("id").GetGuid())];

    private static Task<JsonElement> Apontar(
        Equipe equipe, Guid tarefa, int minutos, bool visivelCliente, string? descricaoExterna = null) =>
        equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/apontamentos", new
        {
            tarefaId = tarefa, data = "2026-10-05", duracaoMinutos = minutos, descricao = "Trabalho na arte",
            descricaoExterna, faturavel = true, visivelCliente,
        });

    private static Task<JsonElement> Resolver(Equipe equipe, Guid solicitacao) =>
        equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/resolucao", new
        {
            solucao = new { texto = "Banner refeito com a data certa" },
        });
}
