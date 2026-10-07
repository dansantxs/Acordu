using Acordu.Domain.Entities;
using static Acordu.Domain.UnitTests.Calendarios;

namespace Acordu.Domain.UnitTests.Entities;

public sealed class AcessoPortalTests
{
    private static readonly TimeSpan TrintaDias = TimeSpan.FromDays(30);

    private static AcessoPortalEmitido Emitir() =>
        AcessoPortal.Emitir(Guid.NewGuid(), Calendarios.Tenant, Contratos.Cliente, Guid.NewGuid(), Guid.NewGuid(), Brasilia(6, 10), TrintaDias);

    [Fact]
    public void Emitir_GuardaSoOHashEDevolveOToken()
    {
        var (acesso, token) = Emitir();

        token.Should().HaveLength(43);
        token.Should().MatchRegex("^[A-Za-z0-9_-]+$");
        acesso.TokenHash.Should().HaveLength(64);
        acesso.TokenHash.Should().NotContain(token);
        acesso.TokenHash.Should().Be(AcessoPortal.HashDoToken(token));
        acesso.ExpiraEm.Should().Be(Brasilia(6, 10) + TrintaDias);
        acesso.Revogado.Should().BeFalse();
    }

    [Fact]
    public void Emitir_DuasVezes_TokensDiferentes()
    {
        Emitir().Token.Should().NotBe(Emitir().Token);
    }

    [Fact]
    public void Emitir_ValidadeNaoPositiva_LancaExcecao()
    {
        var acao = () => AcessoPortal.Emitir(
            Guid.NewGuid(), Calendarios.Tenant, Contratos.Cliente, Guid.NewGuid(), Guid.NewGuid(), Brasilia(6, 10), TimeSpan.Zero);

        acao.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Validar_TokenCertoDentroDaValidade_Verdadeiro()
    {
        var (acesso, token) = Emitir();

        acesso.Validar(token, Brasilia(20, 10)).Should().BeTrue();
    }

    [Fact]
    public void Validar_TokenErrado_Falso()
    {
        var (acesso, _) = Emitir();

        acesso.Validar(Emitir().Token, Brasilia(6, 11)).Should().BeFalse();
        acesso.Validar("", Brasilia(6, 11)).Should().BeFalse();
    }

    [Fact]
    public void Validar_NoInstanteDaExpiracao_Falso()
    {
        var (acesso, token) = Emitir();

        acesso.Validar(token, acesso.ExpiraEm.AddSeconds(-1)).Should().BeTrue();
        acesso.Validar(token, acesso.ExpiraEm).Should().BeFalse();
    }

    [Fact]
    public void Revogar_InvalidaEMantemPrimeiroInstante()
    {
        var (acesso, token) = Emitir();

        acesso.Revogar(Brasilia(7, 9));
        acesso.Revogar(Brasilia(8, 9));

        acesso.Revogado.Should().BeTrue();
        acesso.RevogadoEm.Should().Be(Brasilia(7, 9));
        acesso.Validar(token, Brasilia(7, 10)).Should().BeFalse();
    }

    [Fact]
    public void EstaValido_AteExpirarOuSerRevogado()
    {
        var (acesso, _) = Emitir();

        acesso.EstaValido(Brasilia(20, 10)).Should().BeTrue();
        acesso.EstaValido(Brasilia(6, 10) + TrintaDias).Should().BeFalse();
        acesso.Revogar(Brasilia(7, 9));
        acesso.EstaValido(Brasilia(7, 10)).Should().BeFalse();
    }
}
