using Acordu.Infrastructure.Seguranca;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>
/// Spec do F14, F14-60 (F14p; design D9, RF-AUT-10): a lista versionada de domínios de e-mail descartável, embutida na
/// Infrastructure, e a comparação pelo domínio e pelos domínios-pai. Sem banco.
/// </summary>
public sealed class DominiosDescartaveisTests
{
    private static readonly DominiosDescartaveis Exemplo = new(
    [
        "# cabeçalho com a origem",
        "",
        "descartavel.test",
        "  Caixa-Temporaria.TEST  ",
        "xn--mlltest-n2a.test", // mülltest.test, como a lista guarda os domínios com acento
    ]);

    [Theory]
    [InlineData("ana@descartavel.test")]
    [InlineData("ana@DESCARTAVEL.test")]
    [InlineData("ana@caixa-temporaria.test")]
    [InlineData("ana@descartavel.test.")]
    public void EhDescartavel_DominioDaLista_Verdadeiro(string email) =>
        Exemplo.EhDescartavel(email).Should().BeTrue();

    [Theory]
    [InlineData("ana@sub.descartavel.test")]
    [InlineData("ana@a.b.descartavel.test")]
    public void EhDescartavel_SubdominioDeUmDaLista_Verdadeiro(string email) =>
        Exemplo.EhDescartavel(email).Should().BeTrue("compara o domínio e os domínios-pai");

    [Fact]
    public void EhDescartavel_DominioComAcento_ComparaNaFormaAscii() =>
        Exemplo.EhDescartavel("ana@mülltest.test").Should().BeTrue();

    [Theory]
    [InlineData("ana@exemplo.com")]
    [InlineData("ana@naodescartavel.test")]
    [InlineData("ana@descartavel.test.br")]
    [InlineData("descartavel.test@exemplo.com")]
    [InlineData("sem-arroba")]
    [InlineData("ana@")]
    [InlineData("ana@test")]
    public void EhDescartavel_ForaDaLista_Falso(string email) =>
        Exemplo.EhDescartavel(email).Should().BeFalse();

    [Fact]
    public void Construir_CabecalhoELinhasVazias_FicamDeFora() =>
        Exemplo.Total.Should().Be(3);

    [Fact] // D9 (a): o arquivo versionado vai embutido no assembly e é lido sem rede
    public void DaListaVersionada_ArquivoEmbutido_CarregaALista()
    {
        var lista = DominiosDescartaveis.DaListaVersionada();

        lista.Total.Should().BeGreaterThan(10_000, "a lista pública copiada tem mais de dez mil domínios");
        lista.EhDescartavel("ana@mailinator.com").Should().BeTrue();
        lista.EhDescartavel("ana@yopmail.com").Should().BeTrue();
        lista.EhDescartavel("ana@guerrillamail.com").Should().BeTrue();
        lista.EhDescartavel("ana@灵.cc").Should().BeTrue("a lista guarda o domínio com acento como xn--5nx.cc");
    }

    [Theory] // D9: os provedores comuns e os domínios dos dados de teste e da demonstração não estão na lista
    [InlineData("ana@gmail.com")]
    [InlineData("ana@outlook.com")]
    [InlineData("ana@hotmail.com")]
    [InlineData("ana@yahoo.com.br")]
    [InlineData("ana@uol.com.br")]
    [InlineData("ana@bol.com.br")]
    [InlineData("ana@icloud.com")]
    [InlineData("ana@exemplo.com")]
    [InlineData("ana@acordu.app.br")]
    [InlineData("ana@exemplo.invalid")]
    public void DaListaVersionada_ProvedorComum_NaoEDescartavel(string email) =>
        DominiosDescartaveis.DaListaVersionada().EhDescartavel(email).Should().BeFalse();
}
