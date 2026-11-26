using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Domain.Exceptions;

namespace Acordu.Domain.UnitTests.Entities;

/// <summary>
/// Spec do F15, F15b (F15-01, F15-03, F15-04, F15-96; design D2 e D9): o código do endereço de entrada nasce com a empresa,
/// diferente em cada uma e sem o nome dela; o recebimento nasce desligado e liga e desliga, menos na demonstração.
/// </summary>
public sealed class TenantRecebimentoEmailTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 6, 13, 0, 0, TimeSpan.Zero);
    private static readonly Guid Dono = Guid.NewGuid();

    private static Tenant Empresa(string nome = "Estúdio Traço") =>
        Tenant.Criar(Guid.NewGuid(), nome, Agora, Dono, TimeSpan.FromDays(30));

    private static Tenant Demonstracao() => Tenant.CriarDemonstracao(Guid.NewGuid(), "Estúdio Aurora", Agora, Dono);

    [Fact]
    public void Criar_EmpresaNova_CodigoSorteadoERecebimentoDesligado() // F15-01
    {
        var tenant = Empresa("padaria");

        CodigoDeEntrada.Valido(tenant.CodigoEntrada).Should().BeTrue();
        tenant.CodigoEntrada.Should().NotContain("padaria", "o código não é formado pelo nome");
        tenant.RecebimentoEmail.Should().BeFalse();
    }

    [Fact]
    public void Criar_DuasEmpresasComOMesmoNome_CodigosDiferentes() // F15-01
    {
        Empresa().CodigoEntrada.Should().NotBe(Empresa().CodigoEntrada);
    }

    [Fact]
    public void CriarDemonstracao_Nova_SemCodigoERecebimentoDesligado() // F15-96
    {
        var tenant = Demonstracao();

        tenant.CodigoEntrada.Should().BeNull();
        tenant.RecebimentoEmail.Should().BeFalse();
    }

    [Fact]
    public void LigarRecebimentoEmail_Desligado_Liga() // F15-03
    {
        var tenant = Empresa();

        tenant.LigarRecebimentoEmail();

        tenant.RecebimentoEmail.Should().BeTrue();
    }

    [Fact]
    public void LigarRecebimentoEmail_JaLigado_ContinuaLigado()
    {
        var tenant = Empresa();
        tenant.LigarRecebimentoEmail();

        tenant.LigarRecebimentoEmail();

        tenant.RecebimentoEmail.Should().BeTrue();
    }

    [Fact]
    public void DesligarRecebimentoEmail_Ligado_DesligaSemMudarOCodigo() // F15-04
    {
        var tenant = Empresa();
        var codigo = tenant.CodigoEntrada;
        tenant.LigarRecebimentoEmail();

        tenant.DesligarRecebimentoEmail();

        tenant.RecebimentoEmail.Should().BeFalse();
        tenant.CodigoEntrada.Should().Be(codigo);
    }

    [Fact]
    public void LigarRecebimentoEmail_Demonstracao_RecusaSemMudar() // F15-96
    {
        var tenant = Demonstracao();

        var ligar = tenant.LigarRecebimentoEmail;

        ligar.Should().Throw<TransicaoInvalidaException>().WithMessage(Tenant.MensagemDemonstracaoSemRecebimento);
        tenant.RecebimentoEmail.Should().BeFalse();
    }

    [Fact]
    public void DesligarRecebimentoEmail_Demonstracao_Recusa() // F15-96, design D9
    {
        var tenant = Demonstracao();

        var desligar = tenant.DesligarRecebimentoEmail;

        desligar.Should().Throw<TransicaoInvalidaException>();
    }

    [Fact]
    public void SortearOutroCodigoEntrada_EmpresaComum_TrocaPorOutroValido()
    {
        var tenant = Empresa();
        var antes = tenant.CodigoEntrada;

        tenant.SortearOutroCodigoEntrada();

        tenant.CodigoEntrada.Should().NotBe(antes);
        CodigoDeEntrada.Valido(tenant.CodigoEntrada).Should().BeTrue();
    }

    [Fact]
    public void SortearOutroCodigoEntrada_Demonstracao_RecusaSemCriarCodigo()
    {
        var tenant = Demonstracao();

        var sortear = tenant.SortearOutroCodigoEntrada;

        sortear.Should().Throw<TransicaoInvalidaException>();
        tenant.CodigoEntrada.Should().BeNull();
    }

    [Fact]
    public void RestaurarDemonstracao_Qualquer_RecebimentoDesligado()
    {
        var tenant = Demonstracao();

        tenant.RestaurarDemonstracao("Estúdio Aurora");

        tenant.RecebimentoEmail.Should().BeFalse();
        tenant.CodigoEntrada.Should().BeNull();
    }
}
