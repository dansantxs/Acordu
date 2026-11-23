using Acordu.Application.Cadastro;

namespace Acordu.Application.UnitTests.Cadastro;

/// <summary>
/// Design do F14, D11 e D12 (F14n): o modo do cadastro e a validade do convite de empresa vêm da configuração, com o modo
/// convite quando a variável falta (fail-closed), e valor inválido impede a subida.
/// </summary>
public sealed class OpcoesCadastroTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void DaConfiguracao_SemModo_Convite(string? modo)
    {
        var opcoes = OpcoesCadastro.DaConfiguracao(modo, null);

        opcoes.Modo.Should().Be(ModoCadastro.Convite);
        opcoes.DiasConvite.Should().Be(7);
        opcoes.ValidadeConvite.Should().Be(TimeSpan.FromDays(7));
    }

    [Theory]
    [InlineData("convite", ModoCadastro.Convite)]
    [InlineData(" CONVITE ", ModoCadastro.Convite)]
    [InlineData("aberto", ModoCadastro.Aberto)]
    [InlineData("Aberto", ModoCadastro.Aberto)]
    public void DaConfiguracao_ModoValido_OProprio(string modo, ModoCadastro esperado)
    {
        OpcoesCadastro.DaConfiguracao(modo, null).Modo.Should().Be(esperado);
    }

    [Theory]
    [InlineData("aberta")]
    [InlineData("true")]
    [InlineData("1")]
    public void DaConfiguracao_ModoInvalido_ImpedeASubida(string modo)
    {
        var acao = () => OpcoesCadastro.DaConfiguracao(modo, null);

        acao.Should().Throw<InvalidOperationException>().WithMessage("*Cadastro__Modo*");
    }

    [Fact]
    public void DaConfiguracao_DiasDoConvite_OProprio()
    {
        OpcoesCadastro.DaConfiguracao("aberto", " 14 ").ValidadeConvite.Should().Be(TimeSpan.FromDays(14));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("sete")]
    public void DaConfiguracao_DiasDoConviteInvalidos_ImpedeASubida(string dias)
    {
        var acao = () => OpcoesCadastro.DaConfiguracao("convite", dias);

        acao.Should().Throw<InvalidOperationException>().WithMessage("*Cadastro__DiasConvite*");
    }
}
