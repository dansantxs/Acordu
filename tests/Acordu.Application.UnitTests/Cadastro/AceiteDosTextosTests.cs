using Acordu.Application.Cadastro;
using Acordu.Domain.Enums;

namespace Acordu.Application.UnitTests.Cadastro;

/// <summary>Spec do F14, F14-80, F14-81, F14-115 e F14-116: o aceite dos textos atrás da publicação (design D10 e D16).</summary>
public sealed class AceiteDosTextosTests
{
    private static readonly OpcoesTextosLegais Publicados = new(true);
    private static readonly OpcoesTextosLegais NaoPublicados = new();

    [Fact] // F14-81
    public void Conferir_PublicadosComAsVersoesVigentes_DevolveVerdadeiro()
    {
        AceiteDosTextos.Conferir(Publicados, TextosLegais.VersaoTermos, $" {TextosLegais.VersaoPrivacidade} ").Should().BeTrue();
    }

    [Theory] // F14-80
    [InlineData(null, "1", AceiteDosTextos.CampoTermos)]
    [InlineData("", "1", AceiteDosTextos.CampoTermos)]
    [InlineData("1", null, AceiteDosTextos.CampoPrivacidade)]
    [InlineData(null, null, AceiteDosTextos.CampoTermos)]
    public void Conferir_PublicadosSemAceite_LancaNoCampo(string? termos, string? privacidade, string campo)
    {
        var acao = () => AceiteDosTextos.Conferir(Publicados, termos, privacidade);

        acao.Should().Throw<ArgumentException>().WithParameterName(campo)
            .Which.Message.Should().StartWith(AceiteDosTextos.AceiteObrigatorio);
    }

    [Theory] // D10: a API não grava versão que não é a vigente
    [InlineData("0", "1", AceiteDosTextos.CampoTermos)]
    [InlineData("1", "99", AceiteDosTextos.CampoPrivacidade)]
    public void Conferir_PublicadosComVersaoDiferente_LancaNoCampo(string termos, string privacidade, string campo)
    {
        var acao = () => AceiteDosTextos.Conferir(Publicados, termos, privacidade);

        acao.Should().Throw<ArgumentException>().WithParameterName(campo)
            .Which.Message.Should().StartWith(AceiteDosTextos.VersaoDesatualizada);
    }

    [Theory] // F14-115 e F14-116
    [InlineData(null, null)]
    [InlineData("1", "1")]
    [InlineData("0", "qualquer")]
    public void Conferir_NaoPublicados_IgnoraEDevolveFalso(string? termos, string? privacidade)
    {
        AceiteDosTextos.Conferir(NaoPublicados, termos, privacidade).Should().BeFalse();
    }

    [Theory] // F14-121: só true ou false; ausente desliga
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("true", true)]
    [InlineData(" TRUE ", true)]
    [InlineData("False", false)]
    public void DaConfiguracao_ValorAceito_LeOBooleano(string? valor, bool esperado)
    {
        OpcoesTextosLegais.DaConfiguracao(valor).Publicados.Should().Be(esperado);
    }

    [Theory] // F14-121: valor inválido não sobe
    [InlineData("1")]
    [InlineData("sim")]
    [InlineData("ligado")]
    public void DaConfiguracao_ValorInvalido_LancaExcecao(string valor)
    {
        var acao = () => OpcoesTextosLegais.DaConfiguracao(valor);

        acao.Should().Throw<InvalidOperationException>().WithMessage("*TextosLegais__Publicados*");
    }

    [Fact]
    public void OpcoesTextosLegais_Padrao_Desligada()
    {
        new OpcoesTextosLegais().Publicados.Should().BeFalse();
    }

    [Fact]
    public void Vigente_CadaTexto_DevolveAConstante()
    {
        TextosLegais.Vigente(TipoTextoLegal.Termos).Should().Be(TextosLegais.VersaoTermos);
        TextosLegais.Vigente(TipoTextoLegal.Privacidade).Should().Be(TextosLegais.VersaoPrivacidade);
        TextosLegais.Todos.Should().Equal(TipoTextoLegal.Termos, TipoTextoLegal.Privacidade);
    }
}
