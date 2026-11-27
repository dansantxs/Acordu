using Acordu.Application.Empresas;
using AwesomeAssertions;

namespace Acordu.Application.UnitTests.Empresas;

/// <summary>ADR 0017: o nome do fornecedor nos textos vem da configuração, com o artigo configurado.</summary>
public class OpcoesFornecedorTests
{
    private const string Modelo =
        "{{A fornecedor}} convidou; fale com {{a fornecedor}}, peça {{à fornecedor}}, área {{da fornecedor}}, "
        + "{{na fornecedor}}, {{pela fornecedor}}; {{fornecedor}}.";

    [Fact]
    public void Texto_SemConfiguracao_NebrysNoFeminino()
    {
        new OpcoesFornecedor().Texto(Modelo).Should().Be(
            "A Nebrys convidou; fale com a Nebrys, peça à Nebrys, área da Nebrys, na Nebrys, pela Nebrys; Nebrys.");
    }

    [Fact]
    public void Texto_ArtigoMasculino_TrocaAsContracoes()
    {
        new OpcoesFornecedor(" Grupo Exemplo ", "O").Texto(Modelo).Should().Be(
            "O Grupo Exemplo convidou; fale com o Grupo Exemplo, peça ao Grupo Exemplo, área do Grupo Exemplo, "
            + "no Grupo Exemplo, pelo Grupo Exemplo; Grupo Exemplo.");
    }

    [Fact]
    public void Texto_SemArtigo_SoAPreposicao()
    {
        new OpcoesFornecedor("Exemplo Ltda", "nenhum").Texto(Modelo).Should().Be(
            "Exemplo Ltda convidou; fale com Exemplo Ltda, peça a Exemplo Ltda, área de Exemplo Ltda, em Exemplo Ltda, "
            + "por Exemplo Ltda; Exemplo Ltda.");
    }

    [Fact]
    public void Texto_MarcadorDesconhecido_Falha()
    {
        var montar = () => new OpcoesFornecedor().Texto("fale {{com fornecedor}}");

        montar.Should().Throw<FormatException>().WithMessage("*{{com fornecedor}}*");
    }

    [Theory]
    [InlineData("as")]
    [InlineData("feminino")]
    public void Construir_ArtigoInvalido_Falha(string artigo)
    {
        var construir = () => new OpcoesFornecedor("Exemplo", artigo);

        construir.Should().Throw<InvalidOperationException>().WithMessage("*Fornecedor__Artigo*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void FaleComOFornecedor_SemCanal_SoONome(string? contato)
    {
        new OpcoesFornecedor("Exemplo", "o", contato).FaleComOFornecedor.Should().Be("fale com o Exemplo");
    }

    [Fact]
    public void FaleComOFornecedor_ComCanal_NomeECanal()
    {
        new OpcoesFornecedor("Exemplo", "o", " contato@exemplo.com ").FaleComOFornecedor
            .Should().Be("fale com o Exemplo: contato@exemplo.com");
    }
}
