using Acordu.Application.Cadastro;
using Acordu.Application.Empresas;

namespace Acordu.Application.UnitTests.Cadastro;

/// <summary>Spec do F14, F14-57 (decisão 14 do G2, design D15): a recusa do documento orienta a falar com a Nebrys.</summary>
public sealed class MensagensDeCadastroTests
{
    [Fact]
    public void DocumentoJaTeveTeste_ComOCanal_CitaOEndereco()
    {
        MensagensDeCadastro.DocumentoJaTeveTeste(new OpcoesFornecedor(contato: "  contato@acordu.app.br "))
            .Should().Be("Este CPF ou CNPJ já teve um período de teste no Acordu. Para testar de novo, fale com a Nebrys: contato@acordu.app.br.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DocumentoJaTeveTeste_SemOCanal_SoFaleComOFornecedor(string? contato)
    {
        MensagensDeCadastro.DocumentoJaTeveTeste(new OpcoesFornecedor(contato: contato))
            .Should().Be("Este CPF ou CNPJ já teve um período de teste no Acordu. Para testar de novo, fale com a Nebrys.");
    }

    [Fact] // F14-45
    public void SoPorConvite_ComOCanal_OrientaAPedirUmANebrys()
    {
        MensagensDeCadastro.SoPorConvite(new OpcoesFornecedor(contato: "contato@acordu.app.br"))
            .Should().Be("No momento, o cadastro no Acordu é só por convite. Para pedir um, fale com a Nebrys: contato@acordu.app.br.");
    }

    [Fact] // F14-45, F14-48: o motivo não é distinguido
    public void ConviteInvalido_SemOCanal_SoFaleComOFornecedor()
    {
        MensagensDeCadastro.ConviteInvalido(new OpcoesFornecedor())
            .Should().Be("Este convite não vale mais: venceu, já foi usado ou foi revogado. Para pedir outro, fale com a Nebrys.");
    }
}
