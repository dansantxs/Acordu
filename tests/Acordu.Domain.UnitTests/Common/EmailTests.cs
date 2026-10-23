using Acordu.Domain.Common;

namespace Acordu.Domain.UnitTests.Common;

public sealed class EmailTests
{
    [Fact]
    public void Normaliza_AparaEPoeEmMinusculas() =>
        Email.Normalizar("  Ana.Prado@Exemplo.com ", "email").Should().Be("ana.prado@exemplo.com");

    [Theory]
    [InlineData("sérgio@exemplo.com")]
    [InlineData("joão.silva@exemplo.com")]
    public void AcentoAntesDaArroba_EhRecusado(string email)
    {
        var acao = () => Email.Normalizar(email, "email");
        acao.Should().Throw<ArgumentException>().WithMessage("*sem acento*").Which.ParamName.Should().Be("email");
    }

    [Fact]
    public void DominioComAcento_ContinuaAceito() =>
        Email.Normalizar("contato@açaí.com.br", "email").Should().Be("contato@açaí.com.br");

    [Theory]
    [InlineData("sem-arroba.com")]
    [InlineData("a@b")]
    [InlineData("a b@exemplo.com")]
    public void FormatoInvalido_EhRecusado(string email) =>
        ((Action)(() => Email.Normalizar(email, "email"))).Should().Throw<ArgumentException>();
}
