using Acordu.Api.Composicao;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// Spec do F8, F8-16: a conferência de fuso e ICU que a API faz ao subir passa num ambiente completo (a CI, como a
/// imagem chiseled extra). A falha na imagem sem tzdata/ICU é verificada pela fumaça do job "Imagem da API".
/// </summary>
public sealed class AmbienteDeExecucaoTests
{
    [Fact]
    public void Conferir_ComTzdataEIcu_NaoLanca()
    {
        var conferir = AmbienteDeExecucao.Conferir;

        conferir.Should().NotThrow();
    }
}
