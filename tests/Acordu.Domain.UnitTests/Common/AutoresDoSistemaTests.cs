using Acordu.Domain.Common;

namespace Acordu.Domain.UnitTests.Common;

/// <summary>Design do F15, D15 (ADR 0016, item 10): o autor fixo da nota do sistema e o nome dele.</summary>
public sealed class AutoresDoSistemaTests
{
    [Fact]
    public void Eh_EntradaEmail_Verdadeiro()
    {
        AutoresDoSistema.Eh(AutoresDoSistema.EntradaEmail).Should().BeTrue();
        AutoresDoSistema.EntradaEmail.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Eh_OutroId_Falso()
    {
        AutoresDoSistema.Eh(Guid.NewGuid()).Should().BeFalse();
        AutoresDoSistema.Eh(Guid.Empty).Should().BeFalse();
    }

    [Fact]
    public void Nome_EntradaEmail_NomeFixoDoDesign()
    {
        AutoresDoSistema.Nome(AutoresDoSistema.EntradaEmail).Should().Be("Acordu (e-mail recebido)");
        AutoresDoSistema.Nome(Guid.NewGuid()).Should().BeNull();
    }
}
