using Acordu.Application.EntradaEmail;

namespace Acordu.Application.UnitTests.EntradaEmail;

/// <summary>Design do F15, D12 (F15-05, F15-19, F15-22): a empresa do destino decide antes de qualquer leitura.</summary>
public sealed class AdmissaoDeEntradaTests
{
    [Fact]
    public void Avaliar_SemEmpresa_Desconhecida() // F15-22
    {
        AdmissaoDeEntrada.Avaliar(null).Should().Be(DesfechoEntrada.Desconhecida);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Avaliar_SoLeitura_SomenteLeituraLigadaOuNao(bool ligado) // F15-19
    {
        AdmissaoDeEntrada.Avaliar(new EmpresaDeEntrada(Guid.NewGuid(), true, ligado)).Should().Be(DesfechoEntrada.SomenteLeitura);
    }

    [Fact]
    public void Avaliar_Desligada_Desligada() // F15-05
    {
        AdmissaoDeEntrada.Avaliar(new EmpresaDeEntrada(Guid.NewGuid(), false, false)).Should().Be(DesfechoEntrada.Desligada);
    }

    [Fact]
    public void Avaliar_GravavelELigada_Segue()
    {
        AdmissaoDeEntrada.Avaliar(new EmpresaDeEntrada(Guid.NewGuid(), false, true)).Should().BeNull();
    }
}
