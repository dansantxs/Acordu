using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Jobs;
using Acordu.Api.Operacao;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F15, F15i (F15-23, F15-30 a F15-32, F15-37, F15-40 a F15-45, F15-56, F15-86; RF-SOL-11, RF-SOL-13, RF-POR-05,
/// RF-SOL-09; design D3, D6, D12 e §1.2; contrato §18): o contato responde, pelo programa de e-mail, a um aviso da
/// solicitação; a resposta chega ao endereço da conversa pela rota de entrada, como o Worker entrega, e entra na conversa
/// com os mesmos efeitos de uma resposta pelo portal.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class RespostaPorEmailTests(ApiFixture api)
{
    private const string Fronteira = "fronteira-da-resposta";
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

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

    /// <summary>A solicitação aberta pelo e-mail do contato, com o endereço da conversa (lido do banco: a empresa pode não avisar).</summary>
    private async Task<Aberta> AbertaPorEmailAsync(Cenario c, string? de = null)
    {
        var remetente = de ?? c.Email;
        await EntradaPorEmail.EntregarAsync(
            api, c.Endereco, EntradaPorEmail.Mime($"Carlos Prado <{remetente}>", "Forno parou de novo", EntradaPorEmail.NovoMessageId()),
            remetente);
        var lista = await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/solicitacoes?ordem=-abertaEm");
        var item = lista.GetProperty("itens")[0];
        var id = Cenarios.Id(item);
        return new Aberta(id, item.GetProperty("numero").GetInt32(), await ConversaAsync(c, id));
    }

    private async Task<string> ConversaAsync(Cenario c, Guid solicitacao)
    {
        await using var db = api.DaEquipe(c.Equipe.TenantId);
        var token = (await db.ConversasPorEmail.AsNoTracking().SingleAsync(x => x.SolicitacaoId == solicitacao)).Token;
        return c.Endereco.Replace("@", $"+{token}@", StringComparison.Ordinal);
    }

    /// <summary>A resposta do contato, como o programa de e-mail dele monta: <c>Re:</c> do aviso, para o endereço da conversa.</summary>
    private static byte[] Resposta(
        string de, Aberta aberta, string corpo = "Liguei de novo e nada.", string? messageId = null, string cabecalhos = "") =>
        EntradaPorEmail.Mime(
            $"Carlos Prado <{de}>", $"Re: Re: Forno parou de novo (#{aberta.Numero})", messageId ?? EntradaPorEmail.NovoMessageId(),
            corpo, cabecalhos: cabecalhos, para: aberta.Conversa);

    private async Task ResponderAsync(Cenario c, Aberta aberta, byte[] mime, string? autenticidade = EntradaPorEmail.Autenticado) =>
        await EntradaPorEmail.EntregarAsync(api, aberta.Conversa, mime, c.Email, autenticidade);

    private static async Task<List<JsonElement>> InteracoesAsync(Equipe equipe, Guid solicitacao) =>
        [.. (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}/interacoes"))
            .EnumerateArray()];

    private static async Task<JsonElement> DetalheAsync(Equipe equipe, Guid solicitacao) =>
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/solicitacoes/{solicitacao}");

    private static async Task<List<JsonElement>> SeguidasAsync(Equipe equipe, string? token = null) =>
        [.. (await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/notificacoes", token: token))
            .GetProperty("itens").EnumerateArray().Where(n => n.GetProperty("categoria").GetString() == "SEGUIDAS")];

    private static async Task ComentarAsync(Equipe equipe, Guid solicitacao, string texto = "Qual o modelo do forno?") =>
        await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/interacoes",
            new { texto, visibilidade = "EXTERNA" });

    [Fact] // F15-30 (e a resposta ao aviso de solicitação INTERNA, que o F15-51 diz cair aqui)
    public async Task Responder_EmAndamento_MensagemExternaDoContatoNoCanalEmailEOResponsavelEOsSeguidoresAvisados()
    {
        var c = await CenarioAsync();
        var membro = await c.Equipe.ComPapelAsync(Papel.NomeMembro);
        var eu = await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/auth/eu", token: membro);
        var id = Cenarios.Id(await c.Equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/solicitacoes", new
        {
            clienteId = c.Cliente, contatoId = c.Contato, titulo = "Forno parou de novo", prioridade = "MEDIA",
            responsavelId = eu.GetProperty("usuarioId").GetGuid(),
        }));
        await ComentarAsync(c.Equipe, id);
        await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, "/api/v1/notificacoes/preferencias", new
        {
            categorias = new[] { new { categoria = "SEGUIDAS", email = true } },
        }, membro);
        var numero = (await DetalheAsync(c.Equipe, id)).GetProperty("numero").GetInt32();
        var conversa = api.Emails.Para(c.Email).Last(m => m.Categoria == "aviso-mensagem").ResponderPara!;
        var aberta = new Aberta(id, numero, conversa);

        await ResponderAsync(c, aberta, Resposta(c.Email, aberta, "Liguei de novo e nada.\r\nO modelo é o FX-200."));

        var mensagem = (await InteracoesAsync(c.Equipe, id)).Should().ContainSingle(i => i.GetProperty("autorEhCliente").GetBoolean()).Subject;
        mensagem.GetProperty("autorEhCliente").GetBoolean().Should().BeTrue();
        mensagem.GetProperty("autor").GetProperty("nome").GetString().Should().Be("Carlos Prado");
        mensagem.GetProperty("visibilidade").GetString().Should().Be("EXTERNA");
        mensagem.GetProperty("canal").GetString().Should().Be("EMAIL");
        mensagem.GetProperty("texto").GetString().Should().Be("Liguei de novo e nada.\nO modelo é o FX-200.");
        (await DetalheAsync(c.Equipe, id)).GetProperty("status").GetString().Should().Be("EM_ANDAMENTO");
        var titulo = $"O cliente respondeu em #{numero} Forno parou de novo";
        (await SeguidasAsync(c.Equipe, membro)).Should().ContainSingle(n => n.GetProperty("titulo").GetString() == titulo,
            "o responsável segue a solicitação e é avisado como numa resposta pelo portal");
        (await SeguidasAsync(c.Equipe)).Should().ContainSingle(n => n.GetProperty("titulo").GetString() == titulo,
            "quem abriu e comentou também segue");
        api.Emails.Para(eu.GetProperty("email").GetString()!)
            .Should().ContainSingle(m => m.Categoria == "aviso-seguidas" && m.Assunto == titulo, "o e-mail ao seguidor respeita a preferência dele");
        await using var db = api.DaEquipe(c.Equipe.TenantId);
        var registro = await db.EmailsRecebidos.AsNoTracking().SingleAsync();
        registro.Resultado.Should().Be(ResultadoEntradaEmail.Mensagem);
        registro.SolicitacaoId.Should().Be(id);
    }

    [Fact] // F15-31
    public async Task Responder_AguardandoCliente_MensagemEntraEVoltaAEmAndamento()
    {
        var c = await CenarioAsync();
        var aberta = await AbertaPorEmailAsync(c);
        await ComentarAsync(c.Equipe, aberta.Id);
        await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{aberta.Id}/aguardando-cliente");

        await ResponderAsync(c, aberta, Resposta(c.Email, aberta, "O modelo é o FX-200."));

        (await DetalheAsync(c.Equipe, aberta.Id)).GetProperty("status").GetString().Should().Be("EM_ANDAMENTO", "como no portal (RF-POR-05)");
        (await InteracoesAsync(c.Equipe, aberta.Id)).Should().ContainSingle(i => i.GetProperty("autorEhCliente").GetBoolean())
            .Which.GetProperty("texto").GetString().Should().Be("O modelo é o FX-200.");
    }

    [Fact] // F15-32
    public async Task Responder_Resolvida_MensagemEntraEContinuaResolvida()
    {
        var c = await CenarioAsync();
        var aberta = await AbertaPorEmailAsync(c);
        await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{aberta.Id}/resolucao",
            new { solucao = new { texto = "Resistência trocada" } });
        var resolvidaEm = (await DetalheAsync(c.Equipe, aberta.Id)).GetProperty("resolvidaEm").GetDateTimeOffset();

        await ResponderAsync(c, aberta, Resposta(c.Email, aberta, "Obrigado, mas ainda esquenta pouco."));

        var detalhe = await DetalheAsync(c.Equipe, aberta.Id);
        detalhe.GetProperty("status").GetString().Should().Be("RESOLVIDA", "reabrir é reprovar, com motivo, pelo link do aviso");
        detalhe.GetProperty("resolvidaEm").GetDateTimeOffset().Should().Be(resolvidaEm);
        (await InteracoesAsync(c.Equipe, aberta.Id)).Should().ContainSingle(i => i.GetProperty("autorEhCliente").GetBoolean())
            .Which.GetProperty("texto").GetString().Should().Be("Obrigado, mas ainda esquenta pouco.");
    }

    [Fact] // F15-45: a resposta não zera nem adia o prazo do fechamento automático
    public async Task Diario_ResolvidaComRespostaPorEmail_FechaNoPrazoContadoDaResolucao()
    {
        var c = await CenarioAsync();
        await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, "/api/v1/configuracoes/portal", new { diasFechamentoAutomatico = 2 });
        var aberta = await AbertaPorEmailAsync(c);
        await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{aberta.Id}/resolucao",
            new { solucao = new { texto = "Resistência trocada" } });
        var resolvidaEm = (await DetalheAsync(c.Equipe, aberta.Id)).GetProperty("resolvidaEm").GetDateTimeOffset();

        // Um dia depois da resolução o contato responde; o diário roda dois dias e uma hora depois da resolução (só um dia
        // e uma hora depois da resposta): se a resposta reiniciasse o prazo, a solicitação ficaria RESOLVIDA.
        var inicio = api.Relogio.Agora;
        JsonElement resultado;
        try
        {
            api.Relogio.Agora = inicio.AddDays(1);
            await ResponderAsync(c, aberta, Resposta(c.Email, aberta, "Ainda esquenta pouco."));
            api.Relogio.Agora = inicio.AddDays(2).AddHours(1);
            using var http = api.NovoHttp();
            using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri("/internal/jobs/diario", UriKind.Relative));
            requisicao.Headers.Add(JobsEndpoints.CabecalhoToken, ApiFixture.TokenJobs);
            using var resposta = await http.SendAsync(requisicao);
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            resultado = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        }
        finally
        {
            api.Relogio.Agora = inicio;
        }

        resultado.GetProperty("fechadas").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        var detalhe = await DetalheAsync(c.Equipe, aberta.Id);
        detalhe.GetProperty("status").GetString().Should().Be("FECHADA");
        detalhe.GetProperty("resolvidaEm").GetDateTimeOffset().Should().Be(resolvidaEm);
        (await InteracoesAsync(c.Equipe, aberta.Id)).Should().Contain(i => i.GetProperty("autorEhCliente").GetBoolean(),
            "a resposta entrou antes do fechamento");
    }

    [Fact] // F15-23
    public async Task Responder_MesmaRespostaDuasVezes_UmaMensagemEOsAvisosUmaVez()
    {
        var c = await CenarioAsync();
        var aberta = await AbertaPorEmailAsync(c);
        await ComentarAsync(c.Equipe, aberta.Id);
        var mime = Resposta(c.Email, aberta);

        await ResponderAsync(c, aberta, mime);
        await ResponderAsync(c, aberta, mime);

        (await InteracoesAsync(c.Equipe, aberta.Id)).Count(i => i.GetProperty("autorEhCliente").GetBoolean()).Should().Be(1);
        (await SeguidasAsync(c.Equipe)).Count(n => n.GetProperty("titulo").GetString()!.StartsWith("O cliente respondeu", StringComparison.Ordinal))
            .Should().Be(1, "o aviso da mensagem sai uma vez");
        await using var db = api.DaEquipe(c.Equipe.TenantId);
        (await db.EmailsRecebidos.CountAsync(e => e.Resultado == ResultadoEntradaEmail.Mensagem)).Should().Be(1);
    }

    [Fact] // F15-23: a corrida de duas entregas iguais também deixa uma mensagem só
    public async Task Responder_MesmaRespostaEmParalelo_UmaMensagemSo()
    {
        var c = await CenarioAsync();
        var aberta = await AbertaPorEmailAsync(c);
        var mime = Resposta(c.Email, aberta);

        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => ResponderAsync(c, aberta, mime)));

        await using var db = api.DaEquipe(c.Equipe.TenantId);
        (await db.Interacoes.CountAsync(i => i.SolicitacaoId == aberta.Id)).Should().Be(1);
        (await db.EmailsRecebidos.CountAsync(e => e.Resultado == ResultadoEntradaEmail.Mensagem)).Should().Be(1);
    }

    [Fact] // F15-56: o próprio contato não recebe aviso da mensagem dele
    public async Task Responder_PorEmail_OProprioContatoNaoRecebeAviso()
    {
        var c = await CenarioAsync();
        var aberta = await AbertaPorEmailAsync(c);
        await ComentarAsync(c.Equipe, aberta.Id);
        var antes = api.Emails.Para(c.Email).Count;

        await ResponderAsync(c, aberta, Resposta(c.Email, aberta));

        (await InteracoesAsync(c.Equipe, aberta.Id)).Should().Contain(i => i.GetProperty("autorEhCliente").GetBoolean());
        api.Emails.Para(c.Email).Should().HaveCount(antes, "como na resposta pelo portal");
    }

    [Fact] // F15-37: "#N" no assunto, sem o endereço da conversa, é e-mail novo
    public async Task Entregar_AssuntoComONumeroAoEnderecoDaEmpresa_AbreOutraSemEntrarNaAnterior()
    {
        var c = await CenarioAsync();
        var aberta = await AbertaPorEmailAsync(c);

        await EntradaPorEmail.EntregarAsync(api, c.Endereco, EntradaPorEmail.Mime(
            $"Carlos Prado <{c.Email}>", $"Re: Forno parou de novo (#{aberta.Numero})", EntradaPorEmail.NovoMessageId(), "Mais uma coisa."),
            c.Email);

        var lista = await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/solicitacoes");
        lista.GetProperty("total").GetInt32().Should().Be(2, "segue as regras de e-mail novo (F15-10)");
        var nova = lista.GetProperty("itens").EnumerateArray().Single(s => Cenarios.Id(s) != aberta.Id);
        (await DetalheAsync(c.Equipe, Cenarios.Id(nova))).GetProperty("descricao").GetString().Should().Be("Mais uma coisa.");
        (await InteracoesAsync(c.Equipe, aberta.Id)).Should().BeEmpty("nada entra na #N");
    }

    [Theory] // F15-40: endereço adulterado ou de conversa que não existe mais; na empresa do endereço, segue como e-mail novo
    [InlineData("adulterado")]
    [InlineData("expurgado")]
    public async Task Responder_EnderecoQueNaoEDeConversaNenhuma_SegueComoEmailNovo(string caso)
    {
        var c = await CenarioAsync();
        var aberta = await AbertaPorEmailAsync(c);
        var destino = aberta.Conversa;
        if (caso == "adulterado")
        {
            var mais = destino.IndexOf('+', StringComparison.Ordinal);
            var trocado = destino[mais + 1] == 'a' ? "b" : "a";
            destino = destino.Remove(mais + 1, 1).Insert(mais + 1, trocado);
        }
        else
        {
            await using var db = api.DaEquipe(c.Equipe.TenantId);
            await db.Database.ExecuteSqlAsync($"DELETE FROM conversas_por_email WHERE solicitacao_id = {aberta.Id}");
        }

        await EntradaPorEmail.EntregarAsync(api, destino, Resposta(c.Email, aberta with { Conversa = destino }), c.Email);

        var lista = await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/solicitacoes");
        lista.GetProperty("total").GetInt32().Should().Be(2, "o remetente é conhecido: abre como e-mail novo");
        (await InteracoesAsync(c.Equipe, aberta.Id)).Should().BeEmpty("nada entra na solicitação do token");
    }

    [Fact] // F15-40: o token de uma conversa da empresa B no endereço da empresa A não leva a B
    public async Task Responder_TokenDeOutraEmpresaNoEnderecoDeA_NadaEmBETriagemEmA()
    {
        var a = await CenarioAsync();
        var b = await CenarioAsync();
        var emB = await AbertaPorEmailAsync(b);
        var codigoA = a.Endereco[..a.Endereco.IndexOf('@', StringComparison.Ordinal)];
        var codigoB = b.Endereco[..b.Endereco.IndexOf('@', StringComparison.Ordinal)];
        var destino = emB.Conversa.Replace(codigoB + "+", codigoA + "+", StringComparison.Ordinal);

        await EntradaPorEmail.EntregarAsync(api, destino, Resposta(b.Email, emB with { Conversa = destino }), b.Email);

        (await InteracoesAsync(b.Equipe, emB.Id)).Should().BeEmpty("nada entra em solicitação de outra empresa");
        await using (var dbB = api.DaEquipe(b.Equipe.TenantId))
            (await dbB.EmailsRecebidos.CountAsync()).Should().Be(1, "só o da abertura em B");
        await using var dbA = api.DaEquipe(a.Equipe.TenantId);
        (await dbA.Solicitacoes.CountAsync()).Should().Be(0);
        var item = await dbA.ItensTriagemEmail.AsNoTracking().SingleAsync();
        item.Motivo.Should().Be(MotivoTriagem.Desconhecido, "em A, o remetente não é contato: segue as regras da triagem");
        (await dbA.EmailsRecebidos.AsNoTracking().SingleAsync()).Resultado.Should().Be(ResultadoEntradaEmail.Triagem);
    }

    [Fact] // F15-41
    public async Task Responder_EmpresaNaoLiberada_MensagemEntraSemAvisoAoContato()
    {
        var c = await CenarioAsync(confirmar: false);
        var aberta = await AbertaPorEmailAsync(c);

        await ResponderAsync(c, aberta, Resposta(c.Email, aberta));

        (await InteracoesAsync(c.Equipe, aberta.Id)).Should().ContainSingle(i => i.GetProperty("autorEhCliente").GetBoolean());
        api.Emails.Para(c.Email).Should().BeEmpty("nenhum aviso sai ao contato, como hoje");
    }

    [Theory] // F15-42
    [InlineData("VENCIDA")]
    [InlineData("CANCELADA")]
    public async Task Responder_EmpresaSoLeitura_NadaGravadoNemRespondido(string situacao)
    {
        var c = await CenarioAsync();
        var aberta = await AbertaPorEmailAsync(c);
        var avisos = api.Emails.Para(c.Email).Count;
        var desde = api.Relogio.Agora.ToUniversalTime();
        await using (var db = api.DaEquipe(c.Equipe.TenantId))
        {
            await db.Database.ExecuteSqlAsync(
                $"UPDATE tenants SET situacao = {situacao}, somente_leitura_desde = {desde} WHERE id = {c.Equipe.TenantId}");
        }

        await ResponderAsync(c, aberta, Resposta(c.Email, aberta));

        await using var depois = api.DaEquipe(c.Equipe.TenantId);
        (await depois.Interacoes.CountAsync()).Should().Be(0);
        (await depois.EmailsRecebidos.CountAsync()).Should().Be(1, "só o da abertura");
        api.Emails.Para(c.Email).Should().HaveCount(avisos, "nada é respondido");
    }

    [Fact] // F15-43
    public async Task Responder_RecebimentoDesligadoDepoisDoAviso_NadaGravado()
    {
        var c = await CenarioAsync();
        var aberta = await AbertaPorEmailAsync(c);
        api.Emails.Para(c.Email).Should().Contain(m => m.ResponderPara == aberta.Conversa, "o aviso saiu com o recebimento ligado");
        await EntradaPorEmail.LigarAsync(c.Equipe, ligado: false);

        await ResponderAsync(c, aberta, Resposta(c.Email, aberta));

        await using var db = api.DaEquipe(c.Equipe.TenantId);
        (await db.Interacoes.CountAsync()).Should().Be(0);
        (await db.EmailsRecebidos.CountAsync()).Should().Be(1, "só o da abertura (F15-05)");
    }

    [Fact] // F15-44: o contato com conta vê a mensagem como dele, com os anexos, no canal E-mail
    public async Task Portal_RespostaPorEmailDoContatoComConta_VeComoDeleComOsAnexosNoCanalEmail()
    {
        var c = await CenarioAsync();
        var comConta = await ContatosDoPortal.CriarAsync(api, c.Equipe, c.Cliente, "PROPRIAS", "Marina Souza");
        var aberta = await AbertaPorEmailAsync(c, comConta.Email);

        await EntradaPorEmail.EntregarAsync(api, aberta.Conversa, ComFoto(comConta.Email, aberta, "Segue a foto do painel."), comConta.Email);

        var noPortal = await c.Equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/portal/solicitacoes/{aberta.Id}/interacoes", token: comConta.Token);
        var mensagem = noPortal.EnumerateArray().Should().ContainSingle().Subject;
        DeleComAFotoNoCanalEmail(mensagem, "Marina Souza");
    }

    [Fact] // F15-44: pela sessão do link do aviso (contato sem conta), o mesmo
    public async Task Portal_RespostaPorEmailVistaPeloLinkDoAviso_VeComoDeleComOsAnexosNoCanalEmail()
    {
        var c = await CenarioAsync();
        var aberta = await AbertaPorEmailAsync(c);
        var acesso = await TrocarAsync(api.Emails.TokenDe(c.Email, "aviso-recebida"));

        await EntradaPorEmail.EntregarAsync(api, aberta.Conversa, ComFoto(c.Email, aberta, "Segue a foto do painel."), c.Email);

        var noPortal = await c.Equipe.EsperarAsync(
            HttpStatusCode.OK, HttpMethod.Get, $"/api/v1/portal/solicitacoes/{aberta.Id}/interacoes", token: acesso);
        DeleComAFotoNoCanalEmail(noPortal.EnumerateArray().Should().ContainSingle().Subject, "Carlos Prado");
        await using var db = api.DaEquipe(c.Equipe.TenantId);
        var anexo = (await db.Interacoes.AsNoTracking().SingleAsync(i => i.SolicitacaoId == aberta.Id)).Anexos.Should().ContainSingle().Subject;
        anexo.ChaveArmazenamento.Should().StartWith($"{c.Equipe.TenantId}/{aberta.Id}/", "a chave da solicitação (segurança §7)");
        api.Arquivos.Conteudo(anexo.ChaveArmazenamento).Should().Equal(Png);
    }

    [Theory] // F15-86: a resposta automática ao endereço da conversa é ignorada, sem mensagem nem aviso (sem laço)
    [InlineData("Auto-Submitted: auto-replied\r\n")]
    [InlineData("X-Autoreply: yes\r\n")]
    [InlineData("Precedence: bulk\r\n")]
    public async Task RespostaAutomatica_AoEnderecoDaConversa_IgnoradaSemMensagemNemAviso(string cabecalho)
    {
        var c = await CenarioAsync();
        var aberta = await AbertaPorEmailAsync(c);
        await ComentarAsync(c.Equipe, aberta.Id);
        var aoContato = api.Emails.Para(c.Email).Count;
        var aEquipe = api.Emails.Para(c.Equipe.EmailDono).Count;
        var seguidas = (await SeguidasAsync(c.Equipe)).Count;

        await ResponderAsync(c, aberta, Resposta(c.Email, aberta, "Estou de férias até o dia 20.", cabecalhos: cabecalho));

        (await InteracoesAsync(c.Equipe, aberta.Id)).Should().NotContain(i => i.GetProperty("autorEhCliente").GetBoolean());
        api.Emails.Para(c.Email).Should().HaveCount(aoContato, "nenhum novo aviso sai");
        api.Emails.Para(c.Equipe.EmailDono).Should().HaveCount(aEquipe);
        (await SeguidasAsync(c.Equipe)).Should().HaveCount(seguidas);
        await using var db = api.DaEquipe(c.Equipe.TenantId);
        (await db.EmailsRecebidos.CountAsync(e => e.Resultado == ResultadoEntradaEmail.Ignorado)).Should().Be(1);
    }

    [Theory] // até o F15j (design §3): quem não é o contato da solicitação, a encerrada e a vazia ficam só no registro
    [InlineData("colega")]
    [InlineData("outro-cliente")]
    [InlineData("sem-autenticidade")]
    [InlineData("contato-inativo")]
    [InlineData("cancelada")]
    [InlineData("vazia")]
    public async Task Responder_QuemOuOQueOF15jTrata_SoNoRegistroSemMensagemNemTriagem(string caso)
    {
        var c = await CenarioAsync();
        var aberta = await AbertaPorEmailAsync(c);
        var remetente = c.Email;
        var autenticidade = EntradaPorEmail.Autenticado;
        var corpo = "Liguei de novo e nada.";
        switch (caso)
        {
            case "colega":
                remetente = $"marta-{Guid.NewGuid():N}@bomgrao.com.br";
                await c.Equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/clientes/{c.Cliente}/contatos",
                    new { nome = "Marta Lima", email = remetente });
                break;
            case "outro-cliente":
                remetente = $"paulo-{Guid.NewGuid():N}@outrapadaria.com.br";
                var outro = await Cenarios.ClienteAsync(c.Equipe, "Outra Padaria");
                await c.Equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/clientes/{outro}/contatos",
                    new { nome = "Paulo Reis", email = remetente });
                break;
            case "sem-autenticidade":
                autenticidade = "fail";
                break;
            case "contato-inativo":
                await c.Equipe.EsperarAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/v1/clientes/{c.Cliente}/contatos/{c.Contato}");
                break;
            case "cancelada":
                await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{aberta.Id}/cancelamento",
                    new { motivo = "Cliente desistiu" });
                break;
            default:
                corpo = string.Empty;
                break;
        }
        var avisos = api.Emails.Para(c.Email).Count;

        await EntradaPorEmail.EntregarAsync(api, aberta.Conversa, Resposta(remetente, aberta, corpo), remetente, autenticidade);

        await using var db = api.DaEquipe(c.Equipe.TenantId);
        (await db.Interacoes.CountAsync(i => i.AutorEhCliente)).Should().Be(0, "nenhuma mensagem do contato");
        (await db.Solicitacoes.CountAsync()).Should().Be(1, "nada abre solicitação");
        (await db.ItensTriagemEmail.CountAsync()).Should().Be(0, "a triagem da resposta é do F15j");
        (await db.EmailsRecebidos.CountAsync()).Should().Be(2);
        (await db.EmailsRecebidos.CountAsync(e => e.Resultado == ResultadoEntradaEmail.Ignorado)).Should().Be(1, "só o registro");
        api.Emails.Para(c.Email).Should().HaveCount(avisos, "nada é respondido");
    }

    [Fact] // não regressão do portal: a resposta pelo portal volta de AGUARDANDO_CLIENTE, avisa a equipe e não o próprio contato
    public async Task Portal_RespostaPeloPortal_RetomaAvisaAEquipeESemAvisoAoContato()
    {
        var c = await CenarioAsync();
        var comConta = await ContatosDoPortal.CriarAsync(api, c.Equipe, c.Cliente, "PROPRIAS", "Marina Souza");
        var aberta = await AbertaPorEmailAsync(c, comConta.Email);
        await ComentarAsync(c.Equipe, aberta.Id);
        await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{aberta.Id}/aguardando-cliente");
        var avisos = api.Emails.Para(comConta.Email).Count;

        await c.Equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/portal/solicitacoes/{aberta.Id}/interacoes",
            new { texto = "Liguei de novo e nada." }, comConta.Token);

        (await DetalheAsync(c.Equipe, aberta.Id)).GetProperty("status").GetString().Should().Be("EM_ANDAMENTO");
        (await InteracoesAsync(c.Equipe, aberta.Id)).Should().ContainSingle(i => i.GetProperty("autorEhCliente").GetBoolean())
            .Which.GetProperty("canal").GetString().Should().Be("PORTAL");
        (await SeguidasAsync(c.Equipe)).Should().ContainSingle(
            n => n.GetProperty("titulo").GetString() == $"O cliente respondeu em #{aberta.Numero} Forno parou de novo");
        api.Emails.Para(comConta.Email).Should().HaveCount(avisos);
    }

    [Fact] // não regressão do portal: na RESOLVIDA a resposta pelo portal entra e nada muda na resolução
    public async Task Portal_RespostaPeloPortalComSolicitacaoResolvida_EntraEContinuaResolvida()
    {
        var c = await CenarioAsync();
        var comConta = await ContatosDoPortal.CriarAsync(api, c.Equipe, c.Cliente, "PROPRIAS", "Marina Souza");
        var aberta = await AbertaPorEmailAsync(c, comConta.Email);
        await c.Equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{aberta.Id}/resolucao",
            new { solucao = new { texto = "Resistência trocada" } });
        var resolvidaEm = (await DetalheAsync(c.Equipe, aberta.Id)).GetProperty("resolvidaEm").GetDateTimeOffset();

        await c.Equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/portal/solicitacoes/{aberta.Id}/interacoes",
            new { texto = "Ainda esquenta pouco." }, comConta.Token);

        var detalhe = await DetalheAsync(c.Equipe, aberta.Id);
        detalhe.GetProperty("status").GetString().Should().Be("RESOLVIDA");
        detalhe.GetProperty("resolvidaEm").GetDateTimeOffset().Should().Be(resolvidaEm);
        (await InteracoesAsync(c.Equipe, aberta.Id)).Should().ContainSingle(i => i.GetProperty("autorEhCliente").GetBoolean());
    }

    [Fact] // convenções §5 (e o F15-109, fechado no F15n): o log da resposta diz só o desfecho, sem endereço nem token
    public async Task Responder_PorEmail_LogSoComODesfecho()
    {
        var c = await CenarioAsync();
        var aberta = await AbertaPorEmailAsync(c);
        var logs = new LogsCapturados(LogLevel.Information);
        await using var app = await api.NovaApiAsync(
            new Dictionary<string, string?> { ["Logs:Formato"] = "json" },
            servicos: s => s.AddSingleton<ILoggerProvider>(logs));
        logs.Formatador = app.Services.GetServices<ConsoleFormatter>().OfType<FormatadorLogJson>().Single();
        using var http = app.GetTestClient();
        using var conteudo = new ByteArrayContent(Resposta(c.Email, aberta, "Texto confidencial da resposta."));
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("message/rfc822");
        using var pedido = new HttpRequestMessage(HttpMethod.Post, new Uri(EntradaPorEmail.Rota, UriKind.Relative)) { Content = conteudo };
        pedido.Headers.TryAddWithoutValidation("X-Email-Token", ApiFixture.TokenEntrada);
        pedido.Headers.TryAddWithoutValidation("X-Email-Destinatario", aberta.Conversa);
        pedido.Headers.TryAddWithoutValidation("X-Email-Remetente", c.Email);
        pedido.Headers.TryAddWithoutValidation("X-Email-Filtro", "ok");
        pedido.Headers.TryAddWithoutValidation("X-Email-Autenticidade", EntradaPorEmail.Autenticado);

        using var resposta = await http.SendAsync(pedido);

        resposta.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var todas = string.Join('\n', logs.Linhas.Select(l => l.ToString()));
        todas.Should().Contain("Entrada por e-mail: Respondida");
        var token = aberta.Conversa[(aberta.Conversa.IndexOf('+', StringComparison.Ordinal) + 1)..aberta.Conversa.IndexOf('@', StringComparison.Ordinal)];
        todas.Should().NotContain(c.Email).And.NotContain(token).And.NotContain("confidencial").And.NotContain("Forno parou de novo");
    }

    private static void DeleComAFotoNoCanalEmail(JsonElement mensagem, string nome)
    {
        mensagem.GetProperty("daEquipe").GetBoolean().Should().BeFalse("a mensagem é dele");
        mensagem.GetProperty("autorNome").GetString().Should().Be(nome);
        mensagem.GetProperty("canal").GetString().Should().Be("EMAIL");
        mensagem.GetProperty("texto").GetString().Should().Be("Segue a foto do painel.");
        mensagem.GetProperty("anexos").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("nomeArquivo").GetString().Should().Be("painel.png");
    }

    /// <summary>A resposta com o texto e uma foto anexada, em <c>multipart/mixed</c>.</summary>
    private static byte[] ComFoto(string de, Aberta aberta, string texto) =>
        Encoding.UTF8.GetBytes(
            $"From: Carlos Prado <{de}>\r\n"
            + $"To: {aberta.Conversa}\r\n"
            + $"Subject: Re: Forno parou de novo (#{aberta.Numero})\r\n"
            + $"Message-ID: {EntradaPorEmail.NovoMessageId()}\r\n"
            + "Date: Tue, 06 Oct 2026 10:00:00 -0300\r\n"
            + "MIME-Version: 1.0\r\n"
            + $"Content-Type: multipart/mixed; boundary=\"{Fronteira}\"\r\n"
            + "\r\n"
            + $"--{Fronteira}\r\n"
            + "Content-Type: text/plain; charset=utf-8\r\n"
            + "\r\n"
            + texto + "\r\n"
            + $"--{Fronteira}\r\n"
            + "Content-Type: image/png; name=\"painel.png\"\r\n"
            + "Content-Disposition: attachment; filename=\"painel.png\"\r\n"
            + "Content-Transfer-Encoding: base64\r\n"
            + "\r\n"
            + Convert.ToBase64String(Png) + "\r\n"
            + $"--{Fronteira}--\r\n");

    private async Task<string> TrocarAsync(string token)
    {
        using var http = api.NovoHttp();
        using var resposta = await http.PostAsJsonAsync(new Uri("/api/v1/auth/portal/link", UriKind.Relative), new { token });
        var texto = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, "corpo: {0}", texto);
        return JsonDocument.Parse(texto).RootElement.GetProperty("accessToken").GetString()!;
    }

    private sealed record Cenario(Equipe Equipe, Guid Cliente, Guid Contato, string Email, string Endereco);

    /// <param name="Conversa">O endereço da conversa (<c>{código}+{token}@{domínio}</c>), o Reply-To dos avisos.</param>
    private sealed record Aberta(Guid Id, int Numero, string Conversa);
}
