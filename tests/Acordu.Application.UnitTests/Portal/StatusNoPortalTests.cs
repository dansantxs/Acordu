using Acordu.Application.Portal;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;

namespace Acordu.Application.UnitTests.Portal;

/// <summary>A entrega esperando aprovação é "Esperando você" só para quem pode aprovar; o resto segue o domínio.</summary>
public sealed class StatusNoPortalTests
{
    [Fact]
    public void Resolvida_ParaQuemPodeAprovar_EsperaOContato() =>
        StatusNoPortal.Para(StatusSolicitacao.Resolvida, podeAprovar: true).Should().Be(StatusExterno.AguardandoVoce);

    [Fact]
    public void Resolvida_SemPermissaoDeAprovar_FicaConcluida() =>
        StatusNoPortal.Para(StatusSolicitacao.Resolvida, podeAprovar: false).Should().Be(StatusExterno.Concluida);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DemaisStatus_SeguemODominio(bool podeAprovar)
    {
        foreach (var status in Enum.GetValues<StatusSolicitacao>().Where(s => s != StatusSolicitacao.Resolvida))
            StatusNoPortal.Para(status, podeAprovar).Should().Be(Solicitacao.ParaExterno(status));
    }
}
