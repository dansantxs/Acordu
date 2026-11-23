using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;

namespace Acordu.Domain.UnitTests.Entities;

/// <summary>Spec do F14, F14-45, F14-47 a F14-49 e F14-72 a F14-74 (F14n): o convite de empresa da Nebrys (design D12).</summary>
public sealed class ConviteEmpresaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Validade = TimeSpan.FromDays(7);

    [Fact] // F14-72
    public void Emitir_EmailComCaixaEEspacos_PendenteComValidadeESoOHashDoToken()
    {
        var (convite, token) = ConviteEmpresa.Emitir(Guid.NewGuid(), "  Ana@Estudio.COM ", Validade, Agora.ToOffset(TimeSpan.FromHours(-3)));

        convite.Email.Should().Be("ana@estudio.com");
        convite.CriadoEm.Should().Be(Agora);
        convite.CriadoEm.Offset.Should().Be(TimeSpan.Zero);
        convite.EnviadoEm.Should().Be(Agora);
        convite.ExpiraEm.Should().Be(Agora + Validade);
        convite.SituacaoEm(Agora).Should().Be(SituacaoConviteEmpresa.Pendente);
        convite.TokenHash.Should().Be(TokenSeguro.Hash(token)).And.NotBe(token);
        token.Should().HaveLength(43);
        convite.Validar(token, Agora).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("sem-arroba")]
    [InlineData("sérgio@exemplo.com")]
    public void Emitir_EmailInvalido_LancaNoCampoEmail(string email)
    {
        var acao = () => ConviteEmpresa.Emitir(Guid.NewGuid(), email, Validade, Agora);

        acao.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("email");
    }

    [Fact]
    public void Emitir_ValidadeNaoPositiva_LancaExcecao()
    {
        var acao = () => ConviteEmpresa.Emitir(Guid.NewGuid(), "ana@exemplo.com", TimeSpan.Zero, Agora);

        acao.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact] // F14-45
    public void Validar_DepoisDaValidade_FalsoEVencido()
    {
        var (convite, token) = Emitido();

        convite.Validar(token, Agora + Validade).Should().BeFalse();
        convite.SituacaoEm(Agora + Validade).Should().Be(SituacaoConviteEmpresa.Vencido);
        convite.Validar(token, Agora + Validade - TimeSpan.FromSeconds(1)).Should().BeTrue();
    }

    [Theory] // F14-45
    [InlineData(null)]
    [InlineData("")]
    [InlineData("token-que-nao-e-o-do-convite")]
    public void Validar_TokenErradoOuVazio_Falso(string? token)
    {
        var (convite, _) = Emitido();

        convite.Validar(token, Agora).Should().BeFalse();
    }

    [Fact] // F14-47
    public void Usar_TokenEEmailDoConvite_FicaUsado()
    {
        var (convite, token) = Emitido();

        convite.Usar(token, "ana@exemplo.com", Agora.AddHours(1));

        convite.UsadoEm.Should().Be(Agora.AddHours(1));
        convite.SituacaoEm(Agora.AddHours(1)).Should().Be(SituacaoConviteEmpresa.Usado);
        convite.Pendente.Should().BeFalse();
    }

    [Fact] // F14-48
    public void Usar_JaUsado_LancaENaoValeMais()
    {
        var (convite, token) = Emitido();
        convite.Usar(token, "ana@exemplo.com", Agora);

        var acao = () => convite.Usar(token, "ana@exemplo.com", Agora.AddMinutes(1));

        acao.Should().Throw<TransicaoInvalidaException>();
        convite.Validar(token, Agora.AddMinutes(1)).Should().BeFalse();
        convite.UsadoEm.Should().Be(Agora);
    }

    [Fact] // F14-49
    public void Usar_OutroEmail_LancaEContinuaPendente()
    {
        var (convite, token) = Emitido();

        convite.EhDoEmail("bruno@exemplo.com").Should().BeFalse();
        var acao = () => convite.Usar(token, "bruno@exemplo.com", Agora);

        acao.Should().Throw<TransicaoInvalidaException>();
        convite.UsadoEm.Should().BeNull();
        convite.Validar(token, Agora).Should().BeTrue("o convite continua válido");
    }

    [Fact] // F14-73
    public void Reenviar_Pendente_TokenNovoValidadeReiniciadaEOAnteriorParaDeValer()
    {
        var (convite, anterior) = Emitido();
        var depois = Agora.AddDays(3);

        var novo = convite.Reenviar(Validade, depois);

        novo.Should().NotBe(anterior);
        convite.Validar(anterior, depois).Should().BeFalse("o link anterior para de valer");
        convite.Validar(novo, depois).Should().BeTrue();
        convite.EnviadoEm.Should().Be(depois);
        convite.ExpiraEm.Should().Be(depois + Validade);
        convite.CriadoEm.Should().Be(Agora);
    }

    [Fact] // F14-73
    public void Reenviar_Vencido_VoltaAValer()
    {
        var (convite, _) = Emitido();
        var depois = Agora + Validade + TimeSpan.FromDays(2);

        var novo = convite.Reenviar(Validade, depois);

        convite.SituacaoEm(depois).Should().Be(SituacaoConviteEmpresa.Pendente);
        convite.Validar(novo, depois).Should().BeTrue();
    }

    [Fact]
    public void Reenviar_UsadoOuRevogado_TransicaoInvalida()
    {
        var (usado, token) = Emitido();
        usado.Usar(token, "ana@exemplo.com", Agora);
        var (revogado, _) = Emitido();
        revogado.Revogar(Agora);

        ((Action)(() => usado.Reenviar(Validade, Agora))).Should().Throw<TransicaoInvalidaException>().WithMessage("*usado*");
        ((Action)(() => revogado.Reenviar(Validade, Agora))).Should().Throw<TransicaoInvalidaException>().WithMessage("*revogado*");
    }

    [Fact] // F14-74
    public void Revogar_Pendente_ORevogadoNaoValeMais()
    {
        var (convite, token) = Emitido();

        convite.Revogar(Agora.AddHours(2));

        convite.RevogadoEm.Should().Be(Agora.AddHours(2));
        convite.SituacaoEm(Agora.AddHours(2)).Should().Be(SituacaoConviteEmpresa.Revogado);
        convite.Validar(token, Agora.AddHours(2)).Should().BeFalse();
        ((Action)(() => convite.Usar(token, "ana@exemplo.com", Agora.AddHours(2)))).Should().Throw<TransicaoInvalidaException>();
    }

    [Fact]
    public void Revogar_UsadoOuJaRevogado_TransicaoInvalida()
    {
        var (usado, token) = Emitido();
        usado.Usar(token, "ana@exemplo.com", Agora);
        var (revogado, _) = Emitido();
        revogado.Revogar(Agora);

        ((Action)(() => usado.Revogar(Agora))).Should().Throw<TransicaoInvalidaException>();
        ((Action)(() => revogado.Revogar(Agora.AddHours(1)))).Should().Throw<TransicaoInvalidaException>();
        revogado.RevogadoEm.Should().Be(Agora);
    }

    private static (ConviteEmpresa Convite, string Token) Emitido() =>
        ConviteEmpresa.Emitir(Guid.NewGuid(), "ana@exemplo.com", Validade, Agora);
}
