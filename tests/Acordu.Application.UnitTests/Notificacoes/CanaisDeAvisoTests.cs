using Acordu.Application.Abstractions;
using Acordu.Application.Notificacoes.Canais;
using Acordu.Domain.Enums;

namespace Acordu.Application.UnitTests.Notificacoes;

/// <summary>
/// Spec do F15, F15c (F15-51, F15-52, F15-55, F15-57; design D7 e D13; ADR 0016, item 7): o seletor escolhe o canal pela
/// origem, e o canal de e-mail põe o Reply-To, a marcadora e os cabeçalhos de mensagem automática.
/// </summary>
public sealed class CanaisDeAvisoTests
{
    private const string Conversa = "k7m2qdxz+abcdefghjkmnpqrs@entrada.acordu.app.br";
    private const string ResponderPara = "atendimento@estudiotraco.com.br";
    private const string Texto = "Olá, Carlos!\n\nEstúdio Traço escreveu na solicitação #7.";

    private readonly EnviadorFalso _enviador = new();

    private static AvisoAoContato Aviso(
        OrigemSolicitacao origem = OrigemSolicitacao.Interna, string? responderPara = ResponderPara, string? conversa = Conversa) =>
        new("aviso-mensagem", "Carlos Prado", "carlos@bomgrao.com.br", "Estúdio Traço", "Nova mensagem na solicitação #7", Texto,
            Guid.NewGuid(), 7, origem, responderPara, conversa);

    [Fact]
    public async Task EnviarAsync_RecebimentoLigado_ReplyToDaConversaEMarcadoraNoTopo() // F15-51, F15-57
    {
        await new CanalEmail(_enviador).EnviarAsync(Aviso());

        var mensagem = _enviador.Mensagens.Should().ContainSingle().Subject;
        mensagem.ResponderPara.Should().Be(Conversa, "a resposta do contato entra na solicitação, não na caixa do prestador");
        mensagem.Texto.Should().StartWith("— Escreva sua resposta acima desta linha —\n\n");
        mensagem.Texto.Should().EndWith(Texto);
        mensagem.Texto.Split('\n')[0].Should().Be(CanalEmail.LinhaMarcadora);
        mensagem.Para.Should().Be("carlos@bomgrao.com.br");
        mensagem.Assunto.Should().Be("Nova mensagem na solicitação #7");
        mensagem.Categoria.Should().Be("aviso-mensagem");
        mensagem.NomeRemetente.Should().Be("Estúdio Traço");
    }

    [Fact]
    public async Task EnviarAsync_RecebimentoDesligado_ComoAntesComOResponderParaDaEmpresa() // F15-52, F15-57
    {
        await new CanalEmail(_enviador).EnviarAsync(Aviso(conversa: null));

        var mensagem = _enviador.Mensagens.Should().ContainSingle().Subject;
        mensagem.ResponderPara.Should().Be(ResponderPara);
        mensagem.Texto.Should().Be(Texto, "sem o recebimento, o texto não ganha a marcadora");
    }

    [Fact]
    public async Task EnviarAsync_RecebimentoDesligadoSemResponderPara_SemReplyTo() // F15-52
    {
        await new CanalEmail(_enviador).EnviarAsync(Aviso(responderPara: null, conversa: null));

        _enviador.Mensagens.Should().ContainSingle().Which.ResponderPara.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EnviarAsync_LigadoOuDesligado_MarcadoComoMensagemAutomatica(bool ligado) // F15-55
    {
        await new CanalEmail(_enviador).EnviarAsync(Aviso(conversa: ligado ? Conversa : null));

        var cabecalhos = _enviador.Mensagens.Should().ContainSingle().Subject.Cabecalhos;
        (cabecalhos?.GetValueOrDefault("Auto-Submitted")).Should().Be("auto-generated");
        (cabecalhos?.GetValueOrDefault("X-Auto-Response-Suppress")).Should().Be("OOF, AutoReply");
        (cabecalhos?.Count).Should().Be(2);
    }

    [Theory]
    [InlineData(OrigemSolicitacao.Interna)]
    [InlineData(OrigemSolicitacao.Portal)]
    [InlineData(OrigemSolicitacao.Email)]
    public void CanalDaOrigem_InternaPortalOuEmail_Email(OrigemSolicitacao origem)
    {
        SeletorDeCanal.CanalDaOrigem(origem).Should().Be(CanalAviso.Email);
    }

    [Fact]
    public void CanalDaOrigem_Whatsapp_Whatsapp()
    {
        SeletorDeCanal.CanalDaOrigem(OrigemSolicitacao.Whatsapp).Should().Be(CanalAviso.Whatsapp);
    }

    [Theory]
    [InlineData(OrigemSolicitacao.Interna)]
    [InlineData(OrigemSolicitacao.Portal)]
    [InlineData(OrigemSolicitacao.Email)]
    public async Task EnviarAsync_OrigemQueNaoEhWhatsapp_SaiPorEmail(OrigemSolicitacao origem) // F15-51
    {
        var whatsapp = new CanalFalso(CanalAviso.Whatsapp, entrega: true);
        var seletor = new SeletorDeCanal([new CanalEmail(_enviador), whatsapp]);

        await seletor.EnviarAsync(Aviso(origem));

        _enviador.Mensagens.Should().ContainSingle();
        whatsapp.Avisos.Should().BeEmpty();
    }

    [Fact]
    public async Task EnviarAsync_OrigemWhatsappSemImplementacao_SaiPorEmail()
    {
        var seletor = new SeletorDeCanal([new CanalEmail(_enviador)]);

        await seletor.EnviarAsync(Aviso(OrigemSolicitacao.Whatsapp));

        _enviador.Mensagens.Should().ContainSingle();
    }

    [Fact]
    public async Task EnviarAsync_OrigemWhatsappQueEntrega_SoPeloWhatsapp()
    {
        var whatsapp = new CanalFalso(CanalAviso.Whatsapp, entrega: true);
        var seletor = new SeletorDeCanal([new CanalEmail(_enviador), whatsapp]);

        await seletor.EnviarAsync(Aviso(OrigemSolicitacao.Whatsapp));

        whatsapp.Avisos.Should().ContainSingle();
        _enviador.Mensagens.Should().BeEmpty();
    }

    [Fact]
    public async Task EnviarAsync_OrigemWhatsappQueNaoEntrega_ReservaPorEmail()
    {
        var whatsapp = new CanalFalso(CanalAviso.Whatsapp, entrega: false);
        var seletor = new SeletorDeCanal([whatsapp, new CanalEmail(_enviador)]);

        await seletor.EnviarAsync(Aviso(OrigemSolicitacao.Whatsapp));

        whatsapp.Avisos.Should().ContainSingle();
        _enviador.Mensagens.Should().ContainSingle();
    }

    /// <summary>O aviso de recebida de uma solicitação aberta por e-mail, com o fio guardado (design D4).</summary>
    private static AvisoAoContato AvisoNoFio(string? conversa = Conversa, string? messageId = "abc123@mail.bomgrao.com.br") =>
        new("aviso-recebida", "Carlos Prado", "carlos@bomgrao.com.br", "Estúdio Traço", "Recebemos sua solicitação #7",
            "Olá, Carlos Prado!\n\nEstúdio Traço recebeu sua solicitação #7.", Guid.NewGuid(), 7, OrigemSolicitacao.Email,
            ResponderPara, conversa, messageId, "Forno parou de novo");

    [Fact] // F15-16, F15-50
    public async Task EnviarAsync_OrigemEmailComFio_AssuntoReComONumeroETextoAbrindoPeloAssuntoDeSempre()
    {
        await new CanalEmail(_enviador).EnviarAsync(AvisoNoFio());

        var mensagem = _enviador.Mensagens.Should().ContainSingle().Subject;
        mensagem.Assunto.Should().Be("Re: Forno parou de novo (#7)");
        mensagem.Texto.Should().StartWith(CanalEmail.LinhaMarcadora + "\n\nRecebemos sua solicitação #7\n\nOlá, Carlos Prado!",
            "a marcadora no topo (F15-57) e, logo depois, o texto que hoje vai no assunto");
        mensagem.ResponderPara.Should().Be(Conversa, "responde para o endereço da conversa");
    }

    [Fact] // F15-16, F15-50: marcado como resposta ao e-mail que abriu a solicitação
    public async Task EnviarAsync_OrigemEmailComFio_InReplyToEReferencesComOMessageIdDeOrigem()
    {
        await new CanalEmail(_enviador).EnviarAsync(AvisoNoFio());

        var cabecalhos = _enviador.Mensagens.Should().ContainSingle().Subject.Cabecalhos;
        (cabecalhos?.GetValueOrDefault("In-Reply-To")).Should().Be("<abc123@mail.bomgrao.com.br>");
        (cabecalhos?.GetValueOrDefault("References")).Should().Be("<abc123@mail.bomgrao.com.br>");
        (cabecalhos?.GetValueOrDefault("Auto-Submitted")).Should().Be("auto-generated", "continua mensagem automática (F15-55)");
        (cabecalhos?.GetValueOrDefault("X-Auto-Response-Suppress")).Should().Be("OOF, AutoReply");
    }

    [Fact] // design D4: o fio não depende do recebimento; o Reply-To e a marcadora, sim (F15-52)
    public async Task EnviarAsync_OrigemEmailComFioRecebimentoDesligado_NoFioSemMarcadoraComOResponderPara()
    {
        await new CanalEmail(_enviador).EnviarAsync(AvisoNoFio(conversa: null));

        var mensagem = _enviador.Mensagens.Should().ContainSingle().Subject;
        mensagem.Assunto.Should().Be("Re: Forno parou de novo (#7)");
        mensagem.Texto.Should().StartWith("Recebemos sua solicitação #7\n\n");
        mensagem.ResponderPara.Should().Be(ResponderPara);
        (mensagem.Cabecalhos?.ContainsKey("In-Reply-To")).Should().BeTrue();
    }

    [Fact] // design D4: sem Message-ID de origem, o assunto do fio sem os cabeçalhos de resposta
    public async Task EnviarAsync_OrigemEmailSemMessageId_AssuntoDoFioSemInReplyTo()
    {
        await new CanalEmail(_enviador).EnviarAsync(AvisoNoFio(messageId: null));

        var mensagem = _enviador.Mensagens.Should().ContainSingle().Subject;
        mensagem.Assunto.Should().Be("Re: Forno parou de novo (#7)");
        (mensagem.Cabecalhos?.ContainsKey("In-Reply-To")).Should().BeFalse();
        (mensagem.Cabecalhos?.Count).Should().Be(2, "só os de mensagem automática");
    }

    [Fact] // F15-51: as outras origens seguem com os assuntos de hoje, mesmo se viesse um fio
    public async Task EnviarAsync_OrigemInternaComFio_AssuntoDeHojeSemInReplyTo()
    {
        await new CanalEmail(_enviador).EnviarAsync(AvisoNoFio() with { Origem = OrigemSolicitacao.Interna });

        var mensagem = _enviador.Mensagens.Should().ContainSingle().Subject;
        mensagem.Assunto.Should().Be("Recebemos sua solicitação #7");
        (mensagem.Cabecalhos?.ContainsKey("In-Reply-To")).Should().BeFalse();
    }

    [Theory] // design D4
    [InlineData("Forno parou", 7, "Re: Forno parou (#7)")]
    [InlineData("  Re: Forno parou  ", 1042, "Re: Re: Forno parou (#1042)")]
    [InlineData("", 7, "Re: (#7)")]
    public void AssuntoNoFio_AssuntoOriginal_ReComONumero(string original, int numero, string esperado)
    {
        CanalEmail.AssuntoNoFio(original, numero).Should().Be(esperado);
    }

    [Fact]
    public void Criar_SemCanalDeEmail_LancaExcecao()
    {
        var acao = () => new SeletorDeCanal([new CanalFalso(CanalAviso.Whatsapp, entrega: true)]);

        acao.Should().Throw<ArgumentException>().WithParameterName("canais");
    }

    [Fact]
    public void Criar_CanalRepetido_LancaExcecao()
    {
        var acao = () => new SeletorDeCanal([new CanalEmail(_enviador), new CanalEmail(_enviador)]);

        acao.Should().Throw<ArgumentException>().WithParameterName("canais");
    }

    private sealed class EnviadorFalso : IEnviadorEmail
    {
        public List<MensagemEmail> Mensagens { get; } = [];

        public Task EnviarAsync(MensagemEmail mensagem, CancellationToken cancellationToken = default)
        {
            Mensagens.Add(mensagem);
            return Task.CompletedTask;
        }
    }

    private sealed class CanalFalso(CanalAviso canal, bool entrega) : ICanalDeAviso
    {
        public List<AvisoAoContato> Avisos { get; } = [];

        public CanalAviso Canal => canal;

        public Task<bool> EnviarAsync(AvisoAoContato aviso, CancellationToken cancellationToken = default)
        {
            Avisos.Add(aviso);
            return Task.FromResult(entrega);
        }
    }
}
