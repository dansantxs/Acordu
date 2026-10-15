using Acordu.Domain.Services;

namespace Acordu.Domain.UnitTests.Services;

public sealed class MencoesTests
{
    private static readonly Guid Autor = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid Ana = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Rafael = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Extrair_NaOrdemSemRepetirNemOAutor()
    {
        var texto = $"@{{{Rafael}}} e @{{{Ana}}}, vejam. @{{{Rafael}}} de novo e eu @{{{Autor}}}.";

        Mencoes.Extrair(texto, Autor).Should().Equal(Rafael, Ana);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sem menção, só um e-mail ana@estudio.com e @{não-é-guid}")]
    public void Extrair_SemTokens_Vazio(string? texto)
    {
        Mencoes.Extrair(texto, Autor).Should().BeEmpty();
    }

    [Fact]
    public void Extrair_AcimaDoMaximo_LancaExcecao()
    {
        var texto = string.Join(' ', Enumerable.Range(0, Mencoes.Maximo + 1).Select(_ => $"@{{{Guid.NewGuid()}}}"));

        var acao = () => Mencoes.Extrair(texto, Autor);

        acao.Should().Throw<ArgumentException>().WithParameterName("texto");
    }
}
