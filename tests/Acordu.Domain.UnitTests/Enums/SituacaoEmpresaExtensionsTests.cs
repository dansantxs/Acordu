using Acordu.Domain.Enums;

namespace Acordu.Domain.UnitTests.Enums;

/// <summary>RF-EMP-02: VENCIDA e CANCELADA são só leitura; TESTE e ATIVA gravam. Fonte única do critério (F14b).</summary>
public sealed class SituacaoEmpresaExtensionsTests
{
    [Theory]
    [InlineData(SituacaoEmpresa.Vencida, true)]
    [InlineData(SituacaoEmpresa.Cancelada, true)]
    [InlineData(SituacaoEmpresa.Teste, false)]
    [InlineData(SituacaoEmpresa.Ativa, false)]
    public void SomenteLeitura_CadaSituacao_SoVencidaECancelada(SituacaoEmpresa situacao, bool esperado)
    {
        situacao.SomenteLeitura().Should().Be(esperado);
    }

    [Fact]
    public void SituacoesSomenteLeitura_TodoValorDoEnum_ConcordaComOMetodo()
    {
        foreach (var situacao in Enum.GetValues<SituacaoEmpresa>())
            SituacaoEmpresaExtensions.SituacoesSomenteLeitura.Contains(situacao).Should().Be(situacao.SomenteLeitura());
        SituacaoEmpresaExtensions.SituacoesSomenteLeitura.Should()
            .BeEquivalentTo([SituacaoEmpresa.Vencida, SituacaoEmpresa.Cancelada]);
    }
}
