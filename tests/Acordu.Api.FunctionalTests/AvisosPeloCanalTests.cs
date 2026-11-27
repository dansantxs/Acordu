using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Application.Abstractions;
using Acordu.Application.Notificacoes.Canais;
using Acordu.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F15, F15c (F15-51 a F15-55 e F15-57; RF-NOT-06; design D3, D7, D9 e D13; contrato §12a): os avisos ao contato
/// pelo canal de origem, com o endereço da conversa no Reply-To e a marcadora quando o recebimento por e-mail está ligado,
/// e marcados como mensagem automática sempre.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class AvisosPeloCanalTests(ApiFixture api)
{
    private const string Marcadora = "— Escreva sua resposta acima desta linha —";
    private const string ResponderPara = "atendimento@estudiotraco.com.br";
    private static readonly string[] Categorias = ["aviso-recebida", "aviso-mensagem", "aviso-aguardando", "aviso-resolvida"];

    [Fact] // F15-51
    public async Task AvisosDeSolicitacaoInterna_RecebimentoLigado_RespondemParaOEnderecoDaConversa()
    {
        var equipe = await Equipe.CriarAsync(api);
        await ResponderParaAsync(equipe);
        await LigarAsync(equipe, true);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var (contato, email) = await ContatoAsync(equipe, cliente);

        var solicitacao = await AbrirAsync(equipe, cliente, contato);
        await Comentar(equipe, solicitacao, "Pode mandar a logo em alta resolução?", "EXTERNA");

        var (codigo, token) = await ConversaAsync(equipe.TenantId, solicitacao);
        var endereco = $"{codigo}+{token}@{ApiFixture.DominioEntrada}";
        Ultimo(email, "aviso-recebida").ResponderPara.Should().Be(endereco, "a resposta do contato entra na solicitação (F15-30)");
        Ultimo(email, "aviso-mensagem").ResponderPara.Should().Be(endereco, "a mesma conversa em todo aviso da solicitação");
        api.Emails.Para(email).Should().OnlyContain(m => m.ResponderPara != ResponderPara, "o \"responder para\" sai dos avisos");
        await using var db = api.DaEquipe(equipe.TenantId);
        (await db.ConversasPorEmail.CountAsync(c => c.SolicitacaoId == solicitacao)).Should().Be(1);
    }

    [Fact] // F15-51
    public async Task AvisoDeSolicitacaoDoPortal_RecebimentoLigado_RespondeParaOEnderecoDaConversa()
    {
        var equipe = await Equipe.CriarAsync(api);
        await LigarAsync(equipe, true);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);

        var solicitacao = Cenarios.Id(await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, "/api/v1/portal/solicitacoes", new { titulo = "Site fora do ar" }, contato.Token));

        var (codigo, token) = await ConversaAsync(equipe.TenantId, solicitacao);
        Ultimo(contato.Email, "aviso-recebida").ResponderPara.Should().Be($"{codigo}+{token}@{ApiFixture.DominioEntrada}");
    }

    [Fact] // F15-51 (design D3: um token por solicitação)
    public async Task DuasSolicitacoes_RecebimentoLigado_CadaUmaComASuaConversa()
    {
        var equipe = await Equipe.CriarAsync(api);
        await LigarAsync(equipe, true);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var (contato, email) = await ContatoAsync(equipe, cliente);

        var primeira = await AbrirAsync(equipe, cliente, contato);
        var primeiroAviso = Ultimo(email, "aviso-recebida").ResponderPara;
        var segunda = await AbrirAsync(equipe, cliente, contato);
        var segundoAviso = Ultimo(email, "aviso-recebida").ResponderPara;

        var (codigo, tokenDaPrimeira) = await ConversaAsync(equipe.TenantId, primeira);
        var (_, tokenDaSegunda) = await ConversaAsync(equipe.TenantId, segunda);
        tokenDaPrimeira.Should().NotBe(tokenDaSegunda);
        primeiroAviso.Should().Be($"{codigo}+{tokenDaPrimeira}@{ApiFixture.DominioEntrada}");
        segundoAviso.Should().Be($"{codigo}+{tokenDaSegunda}@{ApiFixture.DominioEntrada}");
    }

    [Fact] // F15-52
    public async Task Avisos_RecebimentoDesligado_ComoAntesComOResponderParaENenhumaConversa()
    {
        var equipe = await Equipe.CriarAsync(api);
        await ResponderParaAsync(equipe);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var (contato, email) = await ContatoAsync(equipe, cliente);

        var solicitacao = await AbrirAsync(equipe, cliente, contato);
        await PercorrerAsync(equipe, solicitacao);

        var avisos = api.Emails.Para(email);
        avisos.Select(m => m.Categoria).Should().BeEquivalentTo(Categorias);
        avisos.Should().OnlyContain(m => m.ResponderPara == ResponderPara);
        avisos.Should().OnlyContain(m => m.Texto.StartsWith("Olá, Carlos Prado!", StringComparison.Ordinal));
        avisos.Should().NotContain(m => m.Texto.Contains(Marcadora, StringComparison.Ordinal));
        await using var db = api.DaEquipe(equipe.TenantId);
        (await db.ConversasPorEmail.AnyAsync(c => c.SolicitacaoId == solicitacao)).Should().BeFalse(
            "a conversa só nasce no primeiro aviso com o recebimento ligado (design D9)");
    }

    [Fact] // F15-52
    public async Task Aviso_RecebimentoDesligadoDepoisDeLigado_VoltaAoResponderPara()
    {
        var equipe = await Equipe.CriarAsync(api);
        await ResponderParaAsync(equipe);
        await LigarAsync(equipe, true);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var (contato, email) = await ContatoAsync(equipe, cliente);
        var solicitacao = await AbrirAsync(equipe, cliente, contato);
        Ultimo(email, "aviso-recebida").ResponderPara.Should().Contain("+");

        await LigarAsync(equipe, false);
        await Comentar(equipe, solicitacao, "Pode mandar a logo em alta resolução?", "EXTERNA");

        var mensagem = Ultimo(email, "aviso-mensagem");
        mensagem.ResponderPara.Should().Be(ResponderPara);
        mensagem.Texto.Should().NotContain(Marcadora);
    }

    [Fact] // F15-52
    public async Task Aviso_RecebimentoDesligadoSemResponderPara_SemReplyTo()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var (contato, email) = await ContatoAsync(equipe, cliente);

        await AbrirAsync(equipe, cliente, contato);

        Ultimo(email, "aviso-recebida").ResponderPara.Should().BeNull();
    }

    [Theory] // F15-53
    [InlineData(false)]
    [InlineData(true)]
    public async Task AvisoDeMensagem_NotaInternaEExterna_SoAExternaSaiESoComOProprioTexto(bool ligado)
    {
        var equipe = await Equipe.CriarAsync(api);
        await LigarAsync(equipe, ligado);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var (contato, email) = await ContatoAsync(equipe, cliente);
        var solicitacao = await AbrirAsync(equipe, cliente, contato);

        await Comentar(equipe, solicitacao, "Cliente pediu desconto, ver com o sócio", "INTERNA");
        api.Emails.Para(email).Should().NotContain(m => m.Categoria == "aviso-mensagem", "nota INTERNA nunca gera aviso");
        await Comentar(equipe, solicitacao, "Pode mandar a logo em alta resolução?", "EXTERNA");

        var mensagem = api.Emails.Para(email).Should().ContainSingle(m => m.Categoria == "aviso-mensagem").Subject;
        mensagem.Texto.Should().Contain("Pode mandar a logo em alta resolução?");
        api.Emails.Para(email).Should().NotContain(
            m => m.Texto.Contains("desconto", StringComparison.Ordinal) || m.Assunto.Contains("desconto", StringComparison.Ordinal),
            "nota INTERNA não aparece em e-mail ao contato (RNF-SEG-03)");
    }

    [Theory] // F15-54
    [InlineData(false)]
    [InlineData(true)]
    public async Task AvisoDeSolicitacaoPorEmail_ContatoSemConta_LinkSemSenhaNovoACadaEmail(bool ligado)
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var (_, email) = await ContatoAsync(equipe, cliente);
        var solicitacao = await AbertaPorEmailAsync(equipe, email);
        await LigarAsync(equipe, ligado);
        api.Emails.Para(email).Should().ContainSingle(m => m.Categoria == "aviso-recebida", "a abertura real avisa o contato");

        await Comentar(equipe, solicitacao, "Pode mandar a logo em alta resolução?", "EXTERNA");
        var primeiro = api.Emails.TokenDe(email, "aviso-mensagem");
        (await TrocarAsync(primeiro)).Should().Be(HttpStatusCode.OK, "o aviso traz o link de acesso sem senha");
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/aguardando-cliente");

        var segundo = api.Emails.TokenDe(email, "aviso-aguardando");
        segundo.Should().NotBe(primeiro);
        (await TrocarAsync(segundo)).Should().Be(HttpStatusCode.OK);
        (await TrocarAsync(primeiro)).Should().Be(HttpStatusCode.Unauthorized, "o link é renovado a cada e-mail, como hoje");
        Ultimo(email, "aviso-aguardando").Texto.Should().Contain($"/portal/l/{segundo}");
    }

    [Theory] // F15-55
    [InlineData(false)]
    [InlineData(true)]
    public async Task OsQuatroAvisos_LigadoOuDesligado_MarcadosComoMensagemAutomatica(bool ligado)
    {
        var equipe = await Equipe.CriarAsync(api);
        await LigarAsync(equipe, ligado);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var (contato, email) = await ContatoAsync(equipe, cliente);

        await PercorrerAsync(equipe, await AbrirAsync(equipe, cliente, contato));

        var avisos = api.Emails.Para(email);
        avisos.Select(m => m.Categoria).Should().BeEquivalentTo(Categorias);
        foreach (var aviso in avisos)
        {
            (aviso.Cabecalhos?.GetValueOrDefault("Auto-Submitted")).Should().Be("auto-generated", "aviso '{0}'", aviso.Categoria);
            (aviso.Cabecalhos?.GetValueOrDefault("X-Auto-Response-Suppress")).Should().Be("OOF, AutoReply", "aviso '{0}'", aviso.Categoria);
        }
    }

    [Fact] // F15-55: só os avisos; o convite do portal não muda
    public async Task ConviteDoPortal_ForaDosAvisos_SemCabecalhosAMais()
    {
        var equipe = await Equipe.CriarAsync(api);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var contato = await ContatosDoPortal.CriarAsync(api, equipe, cliente);

        var convites = api.Emails.Para(contato.Email).Where(m => m.Categoria == "convite-portal").ToList();
        convites.Should().NotBeEmpty();
        convites.Should().OnlyContain(m => m.Cabecalhos == null);
    }

    [Fact] // F15-57
    public async Task OsQuatroAvisos_RecebimentoLigado_ComecamPelaMarcadora()
    {
        var equipe = await Equipe.CriarAsync(api);
        await LigarAsync(equipe, true);
        var cliente = await Cenarios.ClienteAsync(equipe);
        var (contato, email) = await ContatoAsync(equipe, cliente);

        await PercorrerAsync(equipe, await AbrirAsync(equipe, cliente, contato));

        var avisos = api.Emails.Para(email);
        avisos.Select(m => m.Categoria).Should().BeEquivalentTo(Categorias);
        avisos.Should().OnlyContain(m => m.Texto.StartsWith(Marcadora + "\n", StringComparison.Ordinal));
        CanalEmail.LinhaMarcadora.Should().Be(Marcadora);
    }

    /// <summary>Recebida, mensagem externa, aguardando e resolvida: os quatro avisos do RF-NOT-01.</summary>
    private static async Task PercorrerAsync(Equipe equipe, Guid solicitacao)
    {
        await Comentar(equipe, solicitacao, "Pode mandar a logo em alta resolução?", "EXTERNA");
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/aguardando-cliente");
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/resolucao",
            new { solucao = new { texto = "Logo trocada" } });
    }

    private static async Task LigarAsync(Equipe equipe, bool ligado) =>
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, "/api/v1/configuracoes/recebimento-email", new { ligado });

    private static async Task ResponderParaAsync(Equipe equipe)
    {
        var empresa = await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/v1/configuracoes/empresa");
        await equipe.EsperarAsync(HttpStatusCode.OK, HttpMethod.Put, "/api/v1/configuracoes/empresa", new
        {
            nome = empresa.GetProperty("nome").GetString(),
            responderPara = ResponderPara,
            slaPadrao = empresa.GetProperty("slaPadrao"),
        });
    }

    private static async Task<Guid> AbrirAsync(Equipe equipe, Guid cliente, Guid contato) =>
        Cenarios.Id(await equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, "/api/v1/solicitacoes", new
        {
            clienteId = cliente, contatoId = contato, titulo = "Trocar a logo do site", prioridade = "MEDIA",
        }));

    /// <summary>
    /// Solicitação de origem EMAIL pela abertura real (F15f): o recebimento é ligado para o e-mail entrar, e quem chama o
    /// deixa ligado ou desligado para os avisos seguintes.
    /// </summary>
    private async Task<Guid> AbertaPorEmailAsync(Equipe equipe, string email)
    {
        await LigarAsync(equipe, true);
        var mime = EntradaPorEmail.Mime($"Carlos Prado <{email}>", "Forno parou", EntradaPorEmail.NovoMessageId());
        await EntradaPorEmail.EntregarAsync(api, await EntradaPorEmail.EnderecoAsync(api, equipe.TenantId), mime, email);
        await using var db = api.DaEquipe(equipe.TenantId);
        var solicitacao = await db.Solicitacoes.AsNoTracking().SingleAsync();
        solicitacao.Origem.Should().Be(OrigemSolicitacao.Email);
        return solicitacao.Id;
    }

    private async Task<(string Codigo, string Token)> ConversaAsync(Guid tenantId, Guid solicitacao)
    {
        await using var db = api.DaEquipe(tenantId);
        var tenant = await db.Tenants.AsNoTracking().SingleAsync();
        var conversa = await db.ConversasPorEmail.AsNoTracking().SingleAsync(c => c.SolicitacaoId == solicitacao);
        conversa.ClienteId.Should().NotBeEmpty();
        return (tenant.CodigoEntrada!, conversa.Token);
    }

    private MensagemEmail Ultimo(string email, string categoria) =>
        api.Emails.Para(email).Last(m => m.Categoria == categoria);

    private static async Task<(Guid Id, string Email)> ContatoAsync(Equipe equipe, Guid cliente)
    {
        var email = $"contato-{Guid.NewGuid():N}@bomgrao.com.br";
        var contato = await equipe.EsperarAsync(
            HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/clientes/{cliente}/contatos", new { nome = "Carlos Prado", email });
        return (Cenarios.Id(contato), email);
    }

    private static Task<JsonElement> Comentar(Equipe equipe, Guid solicitacao, string texto, string visibilidade) =>
        equipe.EsperarAsync(HttpStatusCode.Created, HttpMethod.Post, $"/api/v1/solicitacoes/{solicitacao}/interacoes",
            new { texto, visibilidade });

    private async Task<HttpStatusCode> TrocarAsync(string token)
    {
        using var http = api.NovoHttp();
        using var resposta = await http.PostAsJsonAsync(new Uri("/api/v1/auth/portal/link", UriKind.Relative), new { token });
        return resposta.StatusCode;
    }
}
