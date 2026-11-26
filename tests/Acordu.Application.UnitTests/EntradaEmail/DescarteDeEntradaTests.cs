using Acordu.Application.EntradaEmail;

namespace Acordu.Application.UnitTests.EntradaEmail;

/// <summary>
/// Spec do F15, F15e (F15-85, F15-87, F15-88; design D11 e D12; ADR 0016, item 9): o e-mail automático, o laço e o
/// filtro do provedor são descartados antes da identificação, nessa ordem; o resto segue.
/// </summary>
public sealed class DescarteDeEntradaTests
{
    private static readonly OpcoesEmailEntrada Opcoes = OpcoesEmailEntrada.DaConfiguracao(
        "entrada.acordu.app.br", remetenteDosAvisos: "nao-responda@send.acordu.app.br");

    private static MensagemDeEntrada Mensagem(
        SinaisDeAutomatico? sinais = null, string? envelope = "ana@cliente.com.br", string? filtro = "ok", string from = "ana@cliente.com.br") =>
        new("abc@mail.cliente.com.br", [from], new string('a', 64), sinais, envelope, filtro);

    [Theory]
    [InlineData("auto-submitted")]
    [InlineData("auto-submitted-com-parametro")]
    [InlineData("precedence-bulk")]
    [InlineData("precedence-junk")]
    [InlineData("precedence-list")]
    [InlineData("lista")]
    [InlineData("x-autoreply")]
    [InlineData("aviso-de-entrega")]
    [InlineData("aviso-de-entrega-maiusculas")]
    public void Avaliar_CabecalhoDeAutomatico_Automatica(string caso) // F15-85
    {
        var sinais = caso switch
        {
            "auto-submitted" => new SinaisDeAutomatico(AutoSubmitted: "auto-replied"),
            "auto-submitted-com-parametro" => new SinaisDeAutomatico(AutoSubmitted: "Auto-Generated; owner-email=\"x@y\""),
            "precedence-bulk" => new SinaisDeAutomatico(Precedence: "bulk"),
            "precedence-junk" => new SinaisDeAutomatico(Precedence: "Junk"),
            "precedence-list" => new SinaisDeAutomatico(Precedence: " list "),
            "lista" => new SinaisDeAutomatico(ListaDeDistribuicao: true),
            "x-autoreply" => new SinaisDeAutomatico(RespostaAutomatica: true),
            "aviso-de-entrega" => new SinaisDeAutomatico(TipoDoCorpo: "multipart/report"),
            _ => new SinaisDeAutomatico(TipoDoCorpo: "Multipart/Report"),
        };

        DescarteDeEntrada.Avaliar(Mensagem(sinais), Opcoes).Should().Be(DesfechoEntrada.Automatica, caso);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<>")]
    [InlineData(" < > ")]
    [InlineData("MAILER-DAEMON@mx.cliente.com.br")]
    [InlineData("postmaster@cliente.com.br")]
    [InlineData("noreply@cliente.com.br")]
    [InlineData("no-reply@cliente.com.br")]
    public void Avaliar_EnvelopeVazioOuDeSistemaDeEntrega_Automatica(string envelope) // F15-85: devolução e sistema de entrega
    {
        DescarteDeEntrada.Avaliar(Mensagem(envelope: envelope), Opcoes).Should().Be(DesfechoEntrada.Automatica);
    }

    [Theory]
    [InlineData("Mailer-Daemon@mx.cliente.com.br")]
    [InlineData("postmaster@cliente.com.br")]
    [InlineData("noreply@loja.com.br")]
    public void Avaliar_FromDeSistemaDeEntrega_Automatica(string from) // F15-85
    {
        DescarteDeEntrada.Avaliar(Mensagem(from: from), Opcoes).Should().Be(DesfechoEntrada.Automatica);
    }

    [Theory]
    [InlineData("no")]
    [InlineData(" No ")]
    [InlineData("")]
    [InlineData(null)]
    public void Avaliar_AutoSubmittedNoOuAusente_Segue(string? autoSubmitted)
    {
        DescarteDeEntrada.Avaliar(Mensagem(new SinaisDeAutomatico(AutoSubmitted: autoSubmitted)), Opcoes).Should().BeNull();
    }

    [Theory]
    [InlineData("first-class")]
    [InlineData("normal")]
    public void Avaliar_PrecedenceQueNaoEEmMassa_Segue(string precedencia)
    {
        DescarteDeEntrada.Avaliar(Mensagem(new SinaisDeAutomatico(Precedence: precedencia)), Opcoes).Should().BeNull();
    }

    [Fact]
    public void Avaliar_EmailComumDoCliente_Segue()
    {
        DescarteDeEntrada.Avaliar(Mensagem(new SinaisDeAutomatico(TipoDoCorpo: "multipart/alternative")), Opcoes).Should().BeNull();
    }

    [Fact]
    public void Avaliar_SemEnvelope_SemEsseSinal() // o adaptador não mandou X-Email-Remetente: não é devolução
    {
        DescarteDeEntrada.Avaliar(Mensagem(envelope: null), Opcoes).Should().BeNull();
    }

    [Theory]
    [InlineData("k7m2qd9w@entrada.acordu.app.br")] // endereço de entrada de outra empresa
    [InlineData("k7m2qd9w+abcdefghjkmnpqrs@Entrada.Acordu.App.Br")] // endereço da conversa
    [InlineData("nao-responda@send.acordu.app.br")] // o remetente dos avisos
    [InlineData("outro@SEND.acordu.app.br")] // o mesmo domínio dos avisos
    public void Avaliar_FromDoProprioAcordu_Laco(string from) // F15-88
    {
        DescarteDeEntrada.Avaliar(Mensagem(from: from, envelope: "ana@cliente.com.br"), Opcoes).Should().Be(DesfechoEntrada.Laco);
    }

    [Fact]
    public void Avaliar_EnvelopeDoRemetenteDosAvisos_Laco() // F15-88: o envelope também conta
    {
        DescarteDeEntrada.Avaliar(Mensagem(envelope: "bounces@send.acordu.app.br"), Opcoes).Should().Be(DesfechoEntrada.Laco);
    }

    [Theory]
    [InlineData("ana@acordu.app.br")] // o domínio raiz não é o de entrada nem o dos avisos
    [InlineData("ana@entrada.acordu.app.br.exemplo.com")]
    public void Avaliar_DominioParecido_NaoELaco(string from)
    {
        DescarteDeEntrada.Avaliar(Mensagem(from: from), Opcoes).Should().BeNull();
    }

    [Fact]
    public void Avaliar_SemDominioDeEntradaNemDosAvisos_NaoELaco()
    {
        var opcoes = new OpcoesEmailEntrada();

        DescarteDeEntrada.Avaliar(Mensagem(from: "k7m2qd9w@entrada.acordu.app.br"), opcoes).Should().BeNull();
    }

    [Theory]
    [InlineData("spam")]
    [InlineData("virus")]
    [InlineData(" SPAM ")]
    [InlineData("Virus")]
    public void Avaliar_FiltroSpamOuVirus_Filtrada(string filtro) // F15-87
    {
        DescarteDeEntrada.Avaliar(Mensagem(filtro: filtro), Opcoes).Should().Be(DesfechoEntrada.Filtrada);
    }

    [Theory]
    [InlineData("ok")]
    [InlineData("")]
    [InlineData("desconhecido")]
    [InlineData(null)]
    public void Avaliar_FiltroOkOuDesconhecido_Segue(string? filtro)
    {
        DescarteDeEntrada.Avaliar(Mensagem(filtro: filtro), Opcoes).Should().BeNull();
    }

    [Fact]
    public void Avaliar_AutomaticoDoProprioAcorduEFiltrado_AutomaticaPrimeiro() // a ordem do D12: automático, laço, filtro
    {
        var mensagem = Mensagem(new SinaisDeAutomatico(AutoSubmitted: "auto-generated"), filtro: "spam", from: "nao-responda@send.acordu.app.br");

        DescarteDeEntrada.Avaliar(mensagem, Opcoes).Should().Be(DesfechoEntrada.Automatica);
        DescarteDeEntrada.Avaliar(mensagem with { Sinais = null }, Opcoes).Should().Be(DesfechoEntrada.Laco);
    }
}
