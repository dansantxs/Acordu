using Acordu.Application.Cadastro;
using Acordu.Application.Empresas;

namespace Acordu.Application.UnitTests.Cadastro;

/// <summary>Spec do F14, F14-57 (decisão 14 do G2, design D15): a recusa do documento orienta a falar com a Nebrys.</summary>
public sealed class MensagensDeCadastroTests
{
    [Fact]
    public void DocumentoJaTeveTeste_ComOCanal_CitaOEndereco()
    {
        MensagensDeCadastro.DocumentoJaTeveTeste(new OpcoesNebrys("  contato@acordu.app.br "))
            .Should().Be("Este CPF ou CNPJ já teve um período de teste no Acordu. Para testar de novo, fale com a Nebrys: contato@acordu.app.br.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DocumentoJaTeveTeste_SemOCanal_SoFaleComANebrys(string? contato)
    {
        MensagensDeCadastro.DocumentoJaTeveTeste(new OpcoesNebrys(contato))
            .Should().Be("Este CPF ou CNPJ já teve um período de teste no Acordu. Para testar de novo, fale com a Nebrys.");
    }
}
