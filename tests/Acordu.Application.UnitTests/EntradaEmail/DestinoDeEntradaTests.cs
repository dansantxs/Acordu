using Acordu.Application.EntradaEmail;

namespace Acordu.Application.UnitTests.EntradaEmail;

/// <summary>Design do F15, D2 e D3 (F15-22): o destinatário lido como endereço de entrada, só pelo formato.</summary>
public sealed class DestinoDeEntradaTests
{
    private static readonly OpcoesEmailEntrada Opcoes = OpcoesEmailEntrada.DaConfiguracao("entrada.acordu.app.br");

    [Fact]
    public void Ler_EnderecoDaEmpresa_CodigoSemToken()
    {
        var destino = DestinoDeEntrada.Ler("k7m2qd9w@entrada.acordu.app.br", Opcoes);

        destino.Should().Be(new DestinoDeEntrada("k7m2qd9w@entrada.acordu.app.br", "k7m2qd9w", null));
    }

    [Fact]
    public void Ler_EnderecoDaConversa_CodigoEToken()
    {
        var destino = DestinoDeEntrada.Ler("k7m2qd9w+abcdefghjkmnpqrs@entrada.acordu.app.br", Opcoes);

        destino!.Codigo.Should().Be("k7m2qd9w");
        destino.Token.Should().Be("abcdefghjkmnpqrs");
    }

    [Fact]
    public void Ler_MaiusculasEEspacos_Normalizado()
    {
        var destino = DestinoDeEntrada.Ler("  K7M2QD9W@Entrada.Acordu.App.BR ", Opcoes);

        destino!.Endereco.Should().Be("k7m2qd9w@entrada.acordu.app.br");
        destino.Codigo.Should().Be("k7m2qd9w");
    }

    [Fact]
    public void Ler_MaisSemToken_TokenNulo()
    {
        DestinoDeEntrada.Ler("k7m2qd9w+@entrada.acordu.app.br", Opcoes)!.Token.Should().BeNull();
    }

    [Theory] // F15-22: nada disso pode ser de empresa nenhuma
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("k7m2qd9w")]
    [InlineData("@entrada.acordu.app.br")]
    [InlineData("k7m2qd9w@acordu.app.br")]
    [InlineData("k7m2qd9w@outra.entrada.acordu.app.br")]
    [InlineData("k7m2qd9w@entrada.acordu.app.br.exemplo.com")]
    [InlineData("k7m2qd@entrada.acordu.app.br")]
    [InlineData("k7m2qd9wx@entrada.acordu.app.br")]
    [InlineData("k7m2qd9o@entrada.acordu.app.br")]
    [InlineData("contato@entrada.acordu.app.br")]
    [InlineData("+abcdefghjkmnpqrs@entrada.acordu.app.br")]
    public void Ler_ForaDoFormato_Nulo(string? destinatario)
    {
        DestinoDeEntrada.Ler(destinatario, Opcoes).Should().BeNull();
    }

    [Fact]
    public void Ler_SemDominioConfigurado_Nulo()
    {
        DestinoDeEntrada.Ler("k7m2qd9w@entrada.acordu.app.br", OpcoesEmailEntrada.DaConfiguracao(null)).Should().BeNull();
    }

    [Fact]
    public void Ler_EnderecoLongoDemais_Nulo()
    {
        var longo = "k7m2qd9w+" + new string('a', DestinoDeEntrada.TamanhoMaximo) + "@entrada.acordu.app.br";

        DestinoDeEntrada.Ler(longo, Opcoes).Should().BeNull();
    }
}
