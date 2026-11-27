using Acordu.Application.EntradaEmail;

namespace Acordu.Application.UnitTests.EntradaEmail;

/// <summary>
/// Spec do F15, F15f (F15-11 a F15-13; design §1.2): o título é o assunto sem os prefixos de resposta e de encaminhamento,
/// cortado em 200; sem assunto, "E-mail sem assunto"; a descrição é o texto do corpo, cortado em 10.000.
/// </summary>
public sealed class ConteudoDaAberturaTests
{
    [Theory] // F15-11
    [InlineData("Re: Forno parou", "Forno parou")]
    [InlineData("RE: Fwd: Res: ENC: Forno parou", "Forno parou")]
    [InlineData("fw: TR: aw: WG: Forno parou", "Forno parou")]
    [InlineData("Re[2]: Re : Forno parou", "Forno parou")]
    [InlineData("  Enc:Forno parou  ", "Forno parou")]
    [InlineData("Forno parou: Re: de novo", "Forno parou: Re: de novo")]
    [InlineData("Reunião de segunda", "Reunião de segunda")]
    [InlineData("Forno\r\n parou\tde novo", "Forno parou de novo")]
    public void Titulo_AssuntoComPrefixos_SemOsPrefixosDoComeco(string assunto, string esperado)
    {
        ConteudoDaAbertura.Titulo(assunto).Should().Be(esperado);
    }

    [Fact] // F15-11
    public void Titulo_AssuntoLongo_CortadoEm200()
    {
        var titulo = ConteudoDaAbertura.Titulo("Re: " + new string('a', 250));

        titulo.Should().Be(new string('a', 200));
    }

    [Fact] // F15-11: o corte não parte um emoji ao meio
    public void Titulo_CorteNoMeioDeUmEmoji_NaoParteOPar()
    {
        var titulo = ConteudoDaAbertura.Titulo(new string('a', 199) + "😀b");

        titulo.Should().Be(new string('a', 199));
    }

    [Theory] // F15-12
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Re:")]
    [InlineData("Re: Fwd: ENC:  ")]
    public void Titulo_SemAssunto_EmailSemAssunto(string? assunto)
    {
        ConteudoDaAbertura.Titulo(assunto).Should().Be("E-mail sem assunto");
    }

    [Fact] // F15-13
    public void Descricao_Texto_QuebrasEmBarraNEAparado()
    {
        ConteudoDaAbertura.Descricao("\r\n  Linha 1\r\nLinha 2\rLinha 3\n\n").Should().Be("Linha 1\nLinha 2\nLinha 3");
    }

    [Fact] // F15-13
    public void Descricao_TextoLongo_CortadoEm10Mil()
    {
        ConteudoDaAbertura.Descricao(new string('x', 12_000)).Should().HaveLength(10_000);
    }

    [Theory] // F15-13: corpo vazio deixa a descrição vazia
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \r\n ")]
    public void Descricao_CorpoVazio_Nula(string? texto)
    {
        ConteudoDaAbertura.Descricao(texto).Should().BeNull();
    }

    [Fact] // o caractere nulo não cabe num texto do Postgres
    public void Descricao_ComCaractereNulo_SemEle()
    {
        ConteudoDaAbertura.Descricao("Forno\0 parou").Should().Be("Forno parou");
    }

    [Fact] // design D4: o assunto do fio é o que o contato escreveu, numa linha
    public void AssuntoOriginal_ComPrefixosEQuebras_ComoVeioNumaLinha()
    {
        ConteudoDaAbertura.AssuntoOriginal("  RE: Forno\r\n parou ").Should().Be("RE: Forno parou");
        ConteudoDaAbertura.AssuntoOriginal(null).Should().BeEmpty();
    }
}
