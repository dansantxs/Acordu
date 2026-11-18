using Acordu.Application.Nebrys;

namespace Acordu.Application.UnitTests.Nebrys;

/// <summary>ADR 0014, item 1 (F14k): quem é operador vem só de <c>Nebrys__Operadores</c>.</summary>
public sealed class OperadoresNebrysTests
{
    [Theory] // F14-63: sem a variável, ninguém é operador e a área inteira responde 404
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DaConfiguracao_AusenteOuVazia_NinguemEOperador(string? valor)
    {
        var operadores = OperadoresNebrys.DaConfiguracao(valor);

        operadores.Vazia.Should().BeTrue();
        operadores.Contem("operacao@nebrys.com.br").Should().BeFalse();
    }

    [Fact]
    public void DaConfiguracao_ListaComVirgulaEPontoEVirgula_NormalizaOsEmails()
    {
        var operadores = OperadoresNebrys.DaConfiguracao(" Operacao@Nebrys.com.br , daniel@nebrys.com.br;outra@nebrys.com.br,");

        operadores.Emails.Should().BeEquivalentTo(["operacao@nebrys.com.br", "daniel@nebrys.com.br", "outra@nebrys.com.br"]);
    }

    [Theory]
    [InlineData("operacao@nebrys.com.br")]
    [InlineData("  OPERACAO@nebrys.com.br ")]
    public void Contem_MesmoEmailEmOutraCaixa_Reconhece(string email)
    {
        OperadoresNebrys.DaConfiguracao("operacao@nebrys.com.br").Contem(email).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("dono@empresa.com.br")]
    [InlineData("nao-e-email")]
    public void Contem_ForaDaLista_NaoReconhece(string? email)
    {
        OperadoresNebrys.DaConfiguracao("operacao@nebrys.com.br").Contem(email).Should().BeFalse();
    }

    [Fact]
    public void DaConfiguracao_ItemQueNaoEEmail_ApiNaoSobeSemExporOValor()
    {
        var subir = () => OperadoresNebrys.DaConfiguracao("operacao@nebrys.com.br,segredo-digitado-errado");

        subir.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain(OperadoresNebrys.Chave).And.NotContain("segredo-digitado-errado");
    }
}
