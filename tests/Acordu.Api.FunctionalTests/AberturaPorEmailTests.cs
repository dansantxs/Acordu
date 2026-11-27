using System.Net;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Application.Abstractions;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F15, F15f (F15-10 a F15-13, F15-16 a F15-18, F15-21, F15-27 a F15-29, F15-50, F15-97; RF-SOL-11, RF-NOT-06;
/// design §1.2, D4, D6, D9 e D11; contrato §7, §12a e §18): o e-mail novo de remetente conhecido, entregue pela rota de
/// entrada como o Worker entrega, vira solicitação de origem EMAIL, com o aviso de recebida no fio do e-mail dele e o aviso
/// à equipe, uma vez só por entrega.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class AberturaPorEmailTests(ApiFixture api)
{
    private const string Marcadora = "— Escreva sua resposta acima desta linha —";

    /// <summary>Empresa com o recebimento ligado, um cliente e um contato (sem conta no portal) com e-mail próprio.</summary>
    private async Task<Cenario> CenarioAsync(bool confirmar = true)
    {
        var equipe = await Equipe.CriarAsync(api, confirmar);
        await EntradaPorEmail.LigarAsync(equipe);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var email = $"carlos-{Guid.NewGuid():N}@bomgrao.com.br";
        var contato = Cenarios.Id(await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/clientes/{cliente}/contatos", new { nome = "Carlos Prado", email }));
        return new Cenario(equipe, cliente, contato, email, await EntradaPorEmail.EnderecoAsync(api, equipe.TenantId));
    }

    private static byte[] DoContato(Cenario c, string? assunto, string messageId, string corpo = "O forno parou de novo hoje cedo.") =>
        EntradaPorEmail.Mime($"Carlos Prado <{c.Email}>", assunto, messageId, corpo);

    private async Task EntregarAsync(Cenario c, byte[] mime, string? autenticidade = EntradaPorEmail.Autenticado) =>
        await EntradaPorEmail.EntregarAsync(api, c.Endereco, mime, c.Email, autenticidade);

    private static async Task<JsonElement> UnicaAsync(Equipe equipe)
    {
        var lista = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/solicitacoes");
        lista.GetProperty("total").GetInt32().Should().Be(1);
        var id = Cenarios.Id(lista.GetProperty("itens")[0]);
        return await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{id}");
    }

    private MensagemEmail Ultimo(string email, string categoria) => api.Emails.Para(email).Last(m => m.Categoria == categoria);

    [Fact] // F15-10
    public async Task Entregar_RemetenteConhecido_SolicitacaoAbertaDeOrigemEmailComOClienteEOContato()
    {
        var c = await CenarioAsync();
        var anterior = await Cenarios.SolicitacaoAsync(c.Equipe, c.Cliente);
        var numeroAnterior = (await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{anterior}"))
            .GetProperty("numero").GetInt32();

        await EntregarAsync(c, DoContato(c, "Forno parou de novo", EntradaPorEmail.NovoMessageId()));

        var lista = await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/solicitacoes?status=ABERTA&ordem=-abertaEm");
        var nova = lista.GetProperty("itens").EnumerateArray().Single(s => Cenarios.Id(s) != anterior);
        var detalhe = await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{Cenarios.Id(nova)}");
        detalhe.GetProperty("numero").GetInt32().Should().Be(numeroAnterior + 1, "o próximo protocolo da empresa");
        detalhe.GetProperty("status").GetString().Should().Be("ABERTA");
        detalhe.GetProperty("origem").GetString().Should().Be("EMAIL");
        detalhe.GetProperty("cliente").GetProperty("id").GetGuid().Should().Be(c.Cliente);
        detalhe.GetProperty("contato").GetProperty("id").GetGuid().Should().Be(c.Contato);
        detalhe.GetProperty("contato").GetProperty("nome").GetString().Should().Be("Carlos Prado");
        detalhe.GetProperty("prioridade").GetString().Should().Be("MEDIA");
        detalhe.GetProperty("responsavel").ValueKind.Should().Be(JsonValueKind.Null);
        detalhe.GetProperty("contratoId").ValueKind.Should().Be(JsonValueKind.Null, "sem contrato vigente, como no portal");
        detalhe.GetProperty("sla").ValueKind.Should().Be(JsonValueKind.Null, "sem contrato, sem SLA");
        await using var db = api.DaEquipe(c.Equipe.TenantId);
        (await db.EmailsRecebidos.AsNoTracking().SingleAsync()).Resultado.Should().Be(ResultadoEntradaEmail.Solicitacao);
    }

    [Fact] // F15-10: o contrato como no portal
    public async Task Entregar_ClienteComUmContratoAtivoVigente_SolicitacaoComOContratoEOSla()
    {
        var c = await CenarioAsync();
        var contrato = await Cenarios.ContratoAsync(c.Equipe, c.Cliente);

        await EntregarAsync(c, DoContato(c, "Forno parou de novo", EntradaPorEmail.NovoMessageId()));

        var detalhe = await UnicaAsync(c.Equipe);
        detalhe.GetProperty("contratoId").GetGuid().Should().Be(contrato);
        detalhe.GetProperty("sla").ValueKind.Should().Be(JsonValueKind.Object, "o SLA do contrato vale desde a abertura");
    }

    [Theory] // F15-11
    [InlineData("RE: Fwd: Forno parou de novo", "Forno parou de novo")]
    [InlineData("Enc: res: Re[2]: Forno parou de novo", "Forno parou de novo")]
    public async Task Entregar_AssuntoComPrefixos_TituloSemOsPrefixos(string assunto, string titulo)
    {
        var c = await CenarioAsync();

        await EntregarAsync(c, DoContato(c, assunto, EntradaPorEmail.NovoMessageId()));

        (await UnicaAsync(c.Equipe)).GetProperty("titulo").GetString().Should().Be(titulo);
    }

    [Fact] // F15-11
    public async Task Entregar_AssuntoLongo_TituloCortadoEm200()
    {
        var c = await CenarioAsync();

        await EntregarAsync(c, DoContato(c, "Re: " + new string('a', 300), EntradaPorEmail.NovoMessageId()));

        (await UnicaAsync(c.Equipe)).GetProperty("titulo").GetString().Should().Be(new string('a', 200));
    }

    [Theory] // F15-12
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("Re: Fwd:")]
    public async Task Entregar_SemAssunto_TituloEmailSemAssuntoESolicitacaoCriada(string? assunto)
    {
        var c = await CenarioAsync();

        await EntregarAsync(c, DoContato(c, assunto, EntradaPorEmail.NovoMessageId()));

        (await UnicaAsync(c.Equipe)).GetProperty("titulo").GetString().Should().Be("E-mail sem assunto");
    }

    [Fact] // F15-13: só texto
    public async Task Entregar_CorpoEmTexto_DescricaoEOTexto()
    {
        var c = await CenarioAsync();

        await EntregarAsync(c, DoContato(c, "Forno", EntradaPorEmail.NovoMessageId(), "O forno parou.\r\nLiguei duas vezes."));

        (await UnicaAsync(c.Equipe)).GetProperty("descricao").GetString().Should().Be("O forno parou.\nLiguei duas vezes.");
    }

    [Fact] // F15-13: só HTML vira texto, sem marcação nem script
    public async Task Entregar_CorpoSoEmHtml_DescricaoEmTextoSemMarcacaoNemScript()
    {
        var c = await CenarioAsync();
        var mime = EntradaPorEmail.Mime(
            $"Carlos Prado <{c.Email}>", "Forno", EntradaPorEmail.NovoMessageId(),
            "<html><head><script>alert('oi')</script></head><body><p>O <b>forno</b> parou &amp; esfriou.</p></body></html>",
            "text/html; charset=utf-8");

        await EntregarAsync(c, mime);

        var descricao = (await UnicaAsync(c.Equipe)).GetProperty("descricao").GetString();
        descricao.Should().Contain("O forno parou & esfriou.");
        descricao.Should().NotContain("<").And.NotContain("alert");
    }

    [Fact] // F15-13: texto e HTML, vale o texto
    public async Task Entregar_CorpoEmTextoEHtml_DescricaoDaParteEmTexto()
    {
        var c = await CenarioAsync();
        var corpo = "--b\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nO forno parou (texto).\r\n"
            + "--b\r\nContent-Type: text/html; charset=utf-8\r\n\r\n<p>O forno parou (html).</p>\r\n--b--";
        var mime = EntradaPorEmail.Mime(
            $"Carlos Prado <{c.Email}>", "Forno", EntradaPorEmail.NovoMessageId(), corpo, "multipart/alternative; boundary=\"b\"");

        await EntregarAsync(c, mime);

        (await UnicaAsync(c.Equipe)).GetProperty("descricao").GetString().Should().Be("O forno parou (texto).");
    }

    [Fact] // F15-13: cortada em 10.000; corpo vazio deixa a descrição vazia
    public async Task Entregar_CorpoLongoOuVazio_DescricaoCortadaOuVazia()
    {
        var longo = await CenarioAsync();
        var vazio = await CenarioAsync();

        await EntregarAsync(longo, DoContato(longo, "Forno", EntradaPorEmail.NovoMessageId(), new string('x', 12_000)));
        await EntregarAsync(vazio, DoContato(vazio, "Forno", EntradaPorEmail.NovoMessageId(), string.Empty));

        (await UnicaAsync(longo.Equipe)).GetProperty("descricao").GetString().Should().HaveLength(10_000);
        (await UnicaAsync(vazio.Equipe)).GetProperty("descricao").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact] // F15-16
    public async Task AvisoDeRecebida_EmpresaLiberada_NoFioDoEmailDoContato()
    {
        var c = await CenarioAsync();
        var messageId = EntradaPorEmail.NovoMessageId();

        await EntregarAsync(c, DoContato(c, "Forno parou de novo", messageId));

        var numero = (await UnicaAsync(c.Equipe)).GetProperty("numero").GetInt32();
        var aviso = api.Emails.Para(c.Email).Should().ContainSingle(m => m.Categoria == "aviso-recebida").Subject;
        aviso.Assunto.Should().Be($"Re: Forno parou de novo (#{numero})");
        aviso.Texto.Should().StartWith($"{Marcadora}\n\nRecebemos sua solicitação #{numero}\n\n",
            "o texto começa pelo que hoje vai no assunto, depois da marcadora (F15-57)");
        (aviso.Cabecalhos?.GetValueOrDefault("In-Reply-To")).Should().Be(messageId, "marcado como resposta ao e-mail dele");
        (aviso.Cabecalhos?.GetValueOrDefault("References")).Should().Be(messageId);
        await using var db = api.DaEquipe(c.Equipe.TenantId);
        var conversa = await db.ConversasPorEmail.AsNoTracking().SingleAsync();
        aviso.ResponderPara.Should().Be(c.Endereco.Replace("@", $"+{conversa.Token}@", StringComparison.Ordinal));
        conversa.MessageIdOrigem.Should().Be(messageId.Trim('<', '>'));
        conversa.AssuntoOrigem.Should().Be("Forno parou de novo");
    }

    [Fact] // F15-17
    public async Task AvisoAEquipe_SolicitacaoPorEmail_SinoEEmailComoNaAberturaPeloPortal()
    {
        var c = await CenarioAsync();
        var membro = await c.Equipe.ComPapelAsync(Papel.NomeMembro);
        var emailMembro = await EmailAsync(c.Equipe, membro);
        await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, "/api/v1/notificacoes/preferencias", new
        {
            categorias = new[] { new { categoria = "NOVA_SOLICITACAO", email = false } },
        }, membro);

        await EntregarAsync(c, DoContato(c, "Forno parou de novo", EntradaPorEmail.NovoMessageId()));

        var aviso = api.Emails.Para(c.Equipe.EmailDono).Should().ContainSingle(m => m.Categoria == "aviso-nova-solicitacao").Subject;
        aviso.Assunto.Should().Contain("Forno parou de novo");
        aviso.Texto.Should().Contain("pelo cliente, por e-mail");
        api.Emails.Para(emailMembro).Should().NotContain(m => m.Categoria == "aviso-nova-solicitacao", "o e-mail respeita as preferências");
        foreach (var token in new[] { c.Equipe.Dono, membro })
        {
            var sino = await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/notificacoes", token: token);
            sino.GetProperty("itens").EnumerateArray()
                .Should().ContainSingle(n => n.GetProperty("categoria").GetString() == "NOVA_SOLICITACAO", "o sino sai sempre");
        }
    }

    [Fact] // F15-17 e F15-18: sem liberação, a solicitação nasce, o contato não é avisado e o e-mail interno só vai ao criador
    public async Task Entregar_EmpresaNaoLiberada_CriaSemAvisoDeRecebidaEEmailInternoSoAoCriador()
    {
        var c = await CenarioAsync(confirmar: false);
        var outroDono = await c.Equipe.ComPapelAsync(Papel.NomeDono);
        var emailOutro = await EmailAsync(c.Equipe, outroDono);

        await EntregarAsync(c, DoContato(c, "Forno parou de novo", EntradaPorEmail.NovoMessageId()));

        (await UnicaAsync(c.Equipe)).GetProperty("origem").GetString().Should().Be("EMAIL", "receber não é enviar a terceiros");
        api.Emails.Para(c.Email).Should().BeEmpty("o aviso de recebida não sai, como os demais avisos ao contato");
        api.Emails.Para(c.Equipe.EmailDono).Should().ContainSingle(m => m.Categoria == "aviso-nova-solicitacao");
        api.Emails.Para(emailOutro).Should().NotContain(m => m.Categoria == "aviso-nova-solicitacao");
        var sino = await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/notificacoes", token: outroDono);
        sino.GetProperty("itens").EnumerateArray().Should().Contain(n => n.GetProperty("categoria").GetString() == "NOVA_SOLICITACAO");
    }

    [Fact] // F15-21
    public async Task Entregar_MesmoEmailParaAEB_CadaEmpresaComASuaSolicitacao()
    {
        var a = await CenarioAsync();
        var b = await CenarioAsync();
        var email = a.Email;
        var contatoEmB = Cenarios.Id(await b.Equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/clientes/{b.Cliente}/contatos", new { nome = "Carlos Prado", email }));
        var anteriorEmB = await Cenarios.SolicitacaoAsync(b.Equipe, b.Cliente);
        var numeroAnteriorEmB = (await b.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{anteriorEmB}"))
            .GetProperty("numero").GetInt32();
        var mime = EntradaPorEmail.Mime(
            $"Carlos Prado <{email}>", "Forno parou de novo", EntradaPorEmail.NovoMessageId(),
            para: $"{a.Endereco}, {b.Endereco}");

        // O Email Routing chama o Worker uma vez por destinatário: duas entregas do mesmo MIME.
        await EntradaPorEmail.EntregarAsync(api, a.Endereco, mime, email);
        await EntradaPorEmail.EntregarAsync(api, b.Endereco, mime, email);

        var emA = await UnicaAsync(a.Equipe);
        emA.GetProperty("contato").GetProperty("id").GetGuid().Should().Be(a.Contato);
        var listaB = await b.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/solicitacoes");
        listaB.GetProperty("total").GetInt32().Should().Be(2, "a de B, aberta antes, e a do e-mail");
        var emB = listaB.GetProperty("itens").EnumerateArray().Single(s => s.GetProperty("origem").GetString() == "EMAIL");
        var detalheB = await b.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{Cenarios.Id(emB)}");
        detalheB.GetProperty("contato").GetProperty("id").GetGuid().Should().Be(contatoEmB);
        detalheB.GetProperty("numero").GetInt32().Should().Be(numeroAnteriorEmB + 1, "o protocolo de B, independente do de A");
        await b.Equipe.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"/api/v1/solicitacoes/{Cenarios.Id(emA)}");
        await a.Equipe.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"/api/v1/solicitacoes/{Cenarios.Id(emB)}");
        api.Emails.Para(email).Where(m => m.Categoria == "aviso-recebida").Should().HaveCount(2, "um aviso de cada empresa");
    }

    [Fact] // F15-27: a caixa do prestador redireciona ao endereço de entrada, mantendo o remetente original
    public async Task Entregar_RedirecionadoPelaCaixaDoPrestador_SolicitacaoDoRemetenteOriginal()
    {
        var c = await CenarioAsync();
        // Como chega um redirecionamento (gravado de um Gmail que encaminha automaticamente): o From e o DKIM do
        // remetente original, o To da caixa do prestador, o envelope reescrito em SRS pelo redirecionador.
        var mime = EntradaPorEmail.Mime(
            $"Carlos Prado <{c.Email}>", "Forno parou de novo", EntradaPorEmail.NovoMessageId(),
            cabecalhos: "Received: by mail.estudiotraco.com.br with SMTP id x1; Tue, 06 Oct 2026 10:00:02 -0300\r\n"
                + "X-Forwarded-To: " + c.Endereco + "\r\n"
                + "X-Forwarded-For: atendimento@estudiotraco.com.br " + c.Endereco + "\r\n"
                + "DKIM-Signature: v=1; a=rsa-sha256; d=bomgrao.com.br; s=s1; h=from:to:subject; bh=abc=; b=def=\r\n",
            para: "atendimento@estudiotraco.com.br");
        var envelope = $"atendimento+caf_=SRS0=xYz1=Ab=bomgrao.com.br={c.Email.Split('@')[0]}@estudiotraco.com.br";

        await EntradaPorEmail.EntregarAsync(api, c.Endereco, mime, envelope, EntradaPorEmail.Autenticado);

        var detalhe = await UnicaAsync(c.Equipe);
        detalhe.GetProperty("cliente").GetProperty("id").GetGuid().Should().Be(c.Cliente);
        detalhe.GetProperty("contato").GetProperty("id").GetGuid().Should().Be(c.Contato, "o remetente original, não a caixa do prestador");
        await using var db = api.DaEquipe(c.Equipe.TenantId);
        (await db.EmailsRecebidos.AsNoTracking().SingleAsync()).Resultado.Should().Be(ResultadoEntradaEmail.Solicitacao, "nada vai à triagem");
    }

    [Theory] // remetente conhecido só com o domínio confirmado (D11; a triagem é o F15-26, TriagemEmailTests)
    [InlineData("fail")]
    [InlineData("none")]
    [InlineData(null)]
    public async Task Entregar_AutenticidadeQueNaoConfirma_NenhumaSolicitacaoNemAviso(string? autenticidade)
    {
        var c = await CenarioAsync();

        await EntregarAsync(c, DoContato(c, "Forno parou de novo", EntradaPorEmail.NovoMessageId()), autenticidade);

        await using var db = api.DaEquipe(c.Equipe.TenantId);
        (await db.Solicitacoes.CountAsync()).Should().Be(0);
        (await db.EmailsRecebidos.AsNoTracking().SingleAsync()).Resultado.Should().Be(ResultadoEntradaEmail.Triagem);
        api.Emails.Para(c.Email).Should().BeEmpty("nada é respondido");
    }

    [Fact] // F15-28
    public async Task Portal_AbertaPorEmailPorContatoProprias_SoEleEOsDeTodasDoClienteVeem()
    {
        var c = await CenarioAsync();
        var c1 = await ContatosDoPortal.CriarAsync(api, c.Equipe, c.Cliente, "PROPRIAS", "Carla Um");
        var c2 = await ContatosDoPortal.CriarAsync(api, c.Equipe, c.Cliente, "PROPRIAS", "Caio Dois");
        var todas = await ContatosDoPortal.CriarAsync(api, c.Equipe, c.Cliente, "TODAS_DO_CLIENTE", "Teresa Todas");
        var outroCliente = await Cenarios.ClienteAsync(c.Equipe, "Mercado Central");
        var deOutro = await ContatosDoPortal.CriarAsync(api, c.Equipe, outroCliente, "TODAS_DO_CLIENTE", "Otávio Outro");

        await EntradaPorEmail.EntregarAsync(
            api, c.Endereco, EntradaPorEmail.Mime($"Carla Um <{c1.Email}>", "Forno parou de novo", EntradaPorEmail.NovoMessageId()), c1.Email);

        var id = Cenarios.Id((await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/solicitacoes")).GetProperty("itens")[0]);
        (await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{id}"))
            .GetProperty("contato").GetProperty("id").GetGuid().Should().Be(c1.Id);
        (await IdsNoPortalAsync(c.Equipe, c1.Token)).Should().Contain(id);
        await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/portal/solicitacoes/{id}", token: c1.Token);
        (await IdsNoPortalAsync(c.Equipe, c2.Token)).Should().NotContain(id, "o colega PROPRIAS não vê");
        await c.Equipe.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"/api/v1/portal/solicitacoes/{id}", token: c2.Token);
        (await IdsNoPortalAsync(c.Equipe, todas.Token)).Should().Contain(id, "TODAS_DO_CLIENTE vê");
        (await IdsNoPortalAsync(c.Equipe, deOutro.Token)).Should().NotContain(id);
        await c.Equipe.EsperarAsync(HttpStatusCode.NotFound, HttpMethod.Get, $"/api/v1/portal/solicitacoes/{id}", token: deOutro.Token);
    }

    [Fact] // F15-29
    public async Task ListaEDetalhe_SolicitacaoPorEmail_OrigemEmail()
    {
        var c = await CenarioAsync();
        var interna = await Cenarios.SolicitacaoAsync(c.Equipe, c.Cliente);

        await EntregarAsync(c, DoContato(c, "Forno parou de novo", EntradaPorEmail.NovoMessageId()));

        var itens = (await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/solicitacoes"))
            .GetProperty("itens").EnumerateArray().ToList();
        itens.Should().HaveCount(2);
        itens.Single(s => Cenarios.Id(s) == interna).GetProperty("origem").GetString().Should().Be("INTERNA");
        var porEmail = itens.Single(s => Cenarios.Id(s) != interna);
        porEmail.GetProperty("origem").GetString().Should().Be("EMAIL", "a lista traz a origem");
        (await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{Cenarios.Id(porEmail)}"))
            .GetProperty("origem").GetString().Should().Be("EMAIL");
    }

    [Fact] // F15-50
    public async Task AvisosSeguintes_SolicitacaoPorEmail_TodosNoFioComOProtocoloERespondendoParaAConversa()
    {
        var c = await CenarioAsync();
        var messageId = EntradaPorEmail.NovoMessageId();
        await EntregarAsync(c, DoContato(c, "Fwd: Forno parou de novo", messageId));
        var solicitacao = await UnicaAsync(c.Equipe);
        var id = Cenarios.Id(solicitacao);
        var numero = solicitacao.GetProperty("numero").GetInt32();

        await c.Equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/solicitacoes/{id}/interacoes",
            new { texto = "Qual o modelo do forno?", visibilidade = "EXTERNA" });
        await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{id}/aguardando-cliente");
        await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{id}/resolucao",
            new { solucao = new { texto = "Resistência trocada" } });

        var avisos = api.Emails.Para(c.Email);
        avisos.Select(m => m.Categoria).Should().BeEquivalentTo("aviso-recebida", "aviso-mensagem", "aviso-aguardando", "aviso-resolvida");
        var resposta = avisos[0].ResponderPara;
        resposta.Should().Contain("+").And.EndWith("@" + ApiFixture.DominioEntrada);
        foreach (var aviso in avisos)
        {
            aviso.Assunto.Should().Be($"Re: Fwd: Forno parou de novo (#{numero})", "aviso '{0}'", aviso.Categoria);
            (aviso.Cabecalhos?.GetValueOrDefault("In-Reply-To")).Should().Be(messageId, "aviso '{0}'", aviso.Categoria);
            (aviso.Cabecalhos?.GetValueOrDefault("References")).Should().Be(messageId, "aviso '{0}'", aviso.Categoria);
            aviso.ResponderPara.Should().Be(resposta, "a mesma conversa em todo aviso");
        }

        Ultimo(c.Email, "aviso-mensagem").Texto.Should().StartWith($"{Marcadora}\n\nNova mensagem na solicitação #{numero}\n\n");
        Ultimo(c.Email, "aviso-mensagem").Texto.Should().Contain("Qual o modelo do forno?");
    }

    [Fact] // F15-97
    public async Task Entregar_MesmoEmailDuasVezes_UmaSolicitacaoEOsAvisosUmaVez()
    {
        var c = await CenarioAsync();
        var mime = DoContato(c, "Forno parou de novo", EntradaPorEmail.NovoMessageId());

        await EntregarAsync(c, mime);
        await EntregarAsync(c, mime);

        var lista = await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/solicitacoes");
        lista.GetProperty("total").GetInt32().Should().Be(1, "só uma solicitação");
        api.Emails.Para(c.Email).Where(m => m.Categoria == "aviso-recebida").Should().ContainSingle("o aviso de recebida sai uma vez");
        api.Emails.Para(c.Equipe.EmailDono).Where(m => m.Categoria == "aviso-nova-solicitacao").Should().ContainSingle(
            "o aviso à equipe sai uma vez");
        var sino = await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/notificacoes");
        sino.GetProperty("itens").EnumerateArray().Where(n => n.GetProperty("categoria").GetString() == "NOVA_SOLICITACAO")
            .Should().ContainSingle();
        await using var db = api.DaEquipe(c.Equipe.TenantId);
        (await db.EmailsRecebidos.CountAsync()).Should().Be(1);
        (await db.ConversasPorEmail.CountAsync()).Should().Be(1);
    }

    [Fact] // F15-97: a corrida de duas entregas iguais (o servidor de origem repetindo) também abre uma só
    public async Task Entregar_MesmoEmailEmParalelo_UmaSolicitacaoSo()
    {
        var c = await CenarioAsync();
        var mime = DoContato(c, "Forno parou de novo", EntradaPorEmail.NovoMessageId());

        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => EntregarAsync(c, mime)));

        await using var db = api.DaEquipe(c.Equipe.TenantId);
        (await db.Solicitacoes.CountAsync()).Should().Be(1);
        (await db.EmailsRecebidos.CountAsync()).Should().Be(1);
        api.Emails.Para(c.Email).Where(m => m.Categoria == "aviso-recebida").Should().ContainSingle();
    }

    [Fact] // F15-10 e design D4: sem Message-ID não há a que responder; abre, e os avisos saem com os assuntos de sempre
    public async Task Entregar_SemMessageId_AbreESegueOsAvisosSemFio()
    {
        var c = await CenarioAsync();
        var mime = EntradaPorEmail.Mime($"Carlos Prado <{c.Email}>", "Forno parou de novo", null);

        await EntregarAsync(c, mime);

        var numero = (await UnicaAsync(c.Equipe)).GetProperty("numero").GetInt32();
        var aviso = api.Emails.Para(c.Email).Should().ContainSingle(m => m.Categoria == "aviso-recebida").Subject;
        aviso.Assunto.Should().Be($"Recebemos sua solicitação #{numero}", "sem fio, o assunto de sempre");
        (aviso.Cabecalhos?.ContainsKey("In-Reply-To") ?? false).Should().BeFalse();
        (aviso.Cabecalhos?.ContainsKey("References") ?? false).Should().BeFalse();
        aviso.ResponderPara.Should().Contain("+", "a conversa existe, e a resposta ainda entra nela");
    }

    [Fact] // F15-12 e design D4: sem assunto, o fio fica "Re: (#N)"
    public async Task Entregar_SemAssunto_AvisoDeRecebidaReComOProtocolo()
    {
        var c = await CenarioAsync();

        await EntregarAsync(c, DoContato(c, null, EntradaPorEmail.NovoMessageId()));

        var numero = (await UnicaAsync(c.Equipe)).GetProperty("numero").GetInt32();
        api.Emails.Para(c.Email).Should().ContainSingle(m => m.Categoria == "aviso-recebida").Which.Assunto.Should().Be($"Re: (#{numero})");
    }

    [Fact] // F15-10: o From é normalizado como o e-mail dos contatos (caixa, nome de exibição)
    public async Task Entregar_FromEmMaiusculas_AbreComOContato()
    {
        var c = await CenarioAsync();
        var mime = EntradaPorEmail.Mime(
            $"\"Prado, Carlos\" <{c.Email.ToUpperInvariant()}>", "Forno parou de novo", EntradaPorEmail.NovoMessageId());

        await EntregarAsync(c, mime);

        (await UnicaAsync(c.Equipe)).GetProperty("contato").GetProperty("id").GetGuid().Should().Be(c.Contato);
    }

    [Fact] // F15-13: o caractere nulo no corpo não derruba a entrega (o Postgres não o guarda)
    public async Task Entregar_CorpoComCaractereNulo_CriaSemOCaractere()
    {
        var c = await CenarioAsync();

        await EntregarAsync(c, DoContato(c, "Forno", EntradaPorEmail.NovoMessageId(), "O forno\0 parou."));

        var descricao = (await UnicaAsync(c.Equipe)).GetProperty("descricao").GetString();
        descricao.Should().NotContain("\0").And.Contain("parou");
    }

    [Theory] // remetente que não é "conhecido": vai à triagem (F15h), nenhuma solicitação, nenhuma resposta
    [InlineData("contato-inativo")]
    [InlineData("cliente-inativo")]
    [InlineData("dois-clientes")]
    [InlineData("varios-from")]
    [InlineData("desconhecido")]
    public async Task Entregar_RemetenteNaoIdentificado_NadaAbertoNemRespondido(string caso)
    {
        var c = await CenarioAsync();
        var de = $"Carlos Prado <{c.Email}>";
        switch (caso)
        {
            case "contato-inativo":
                await c.Equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/v1/clientes/{c.Cliente}/contatos/{c.Contato}");
                break;
            case "cliente-inativo":
                await c.Equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/v1/clientes/{c.Cliente}");
                break;
            case "dois-clientes":
                var outroCliente = await Cenarios.ClienteAsync(c.Equipe, "Mercado Central");
                await c.Equipe.EsperarAsync(
                    HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/clientes/{outroCliente}/contatos", new { nome = "Carlos Prado", email = c.Email });
                break;
            case "varios-from":
                de += $", Outra Pessoa <outra-{Guid.NewGuid():N}@bomgrao.com.br>";
                break;
            default:
                de = $"Estranho <estranho-{Guid.NewGuid():N}@outro.com.br>";
                break;
        }

        await EntradaPorEmail.EntregarAsync(
            api, c.Endereco, EntradaPorEmail.Mime(de, "Forno parou de novo", EntradaPorEmail.NovoMessageId()), c.Email);

        await using var db = api.DaEquipe(c.Equipe.TenantId);
        (await db.Solicitacoes.CountAsync()).Should().Be(0);
        (await db.ConversasPorEmail.CountAsync()).Should().Be(0);
        (await db.EmailsRecebidos.AsNoTracking().SingleAsync()).Resultado.Should().Be(ResultadoEntradaEmail.Triagem);
        api.Emails.Para(c.Email).Should().BeEmpty("nada é respondido a quem não é conhecido");
        api.Emails.Para(c.Equipe.EmailDono).Should().NotContain(m => m.Categoria == "aviso-nova-solicitacao");
    }

    private static async Task<List<Guid>> IdsNoPortalAsync(Equipe equipe, string token)
    {
        var lista = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/portal/solicitacoes", token: token);
        return [.. lista.GetProperty("itens").EnumerateArray().Select(Cenarios.Id)];
    }

    private static async Task<string> EmailAsync(Equipe equipe, string token) =>
        (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: token)).GetProperty("email").GetString()!;

    private sealed record Cenario(Equipe Equipe, Guid Cliente, Guid Contato, string Email, string Endereco);
}
