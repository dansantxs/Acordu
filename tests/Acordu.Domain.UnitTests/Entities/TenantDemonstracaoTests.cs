using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.UnitTests.Entities;

/// <summary>
/// Spec do F14, F14s (F14-87, F14-88; RF-NEB-02; design D1 a1 e D13): a empresa de demonstração nasce ATIVA e fora do
/// ciclo de situação, nunca vence nem é expurgada, e a restauração volta a configuração dela ao estado inicial.
/// </summary>
public sealed class TenantDemonstracaoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 6, 13, 0, 0, TimeSpan.Zero);
    private static readonly Guid Dono = Guid.NewGuid();
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    [Fact]
    public void CriarDemonstracao_Nova_AtivaMarcadaSemFimDoTesteNemDocumento() // F14-87
    {
        var id = Guid.NewGuid();

        var tenant = Tenant.CriarDemonstracao(id, "  Estúdio Aurora ", Agora, Dono);

        tenant.Id.Should().Be(id);
        tenant.Nome.Should().Be("Estúdio Aurora");
        tenant.Demonstracao.Should().BeTrue();
        tenant.Situacao.Should().Be(SituacaoEmpresa.Ativa, "fora do ciclo de situação (design D1 a1)");
        tenant.FimDoTeste.Should().BeNull();
        tenant.SomenteLeituraDesde.Should().BeNull();
        tenant.Documento.Should().BeNull();
        tenant.CriadoEm.Should().Be(Agora);
        tenant.CriadoPorUsuarioId.Should().Be(Dono);
        tenant.EnvioLiberado.Should().BeTrue("a tela não pede a confirmação do e-mail fictício; o bloqueio olha a marca");
    }

    [Fact]
    public void CriarDemonstracao_IdOuDonoVazio_LancaExcecao()
    {
        var semId = () => Tenant.CriarDemonstracao(Guid.Empty, "Estúdio Aurora", Agora, Dono);
        var semDono = () => Tenant.CriarDemonstracao(Guid.NewGuid(), "Estúdio Aurora", Agora, Guid.Empty);

        semId.Should().Throw<ArgumentException>().WithParameterName("id");
        semDono.Should().Throw<ArgumentException>().WithParameterName("criadoPorUsuarioId");
    }

    [Fact]
    public void CriarDemonstracao_Transicoes_TodasRecusadas() // F14-69, F14-87
    {
        var tenant = Demonstracao();
        Action[] acoes =
        [
            () => tenant.Ativar(Agora),
            () => tenant.Prorrogar(new DateOnly(2026, 12, 15), SaoPaulo, Agora),
            () => tenant.Cancelar(Agora),
            () => tenant.Reativar(Agora),
            () => tenant.Vencer(Agora.AddYears(5)),
        ];

        foreach (var acao in acoes)
            acao.Should().Throw<TransicaoInvalidaException>();
        tenant.Situacao.Should().Be(SituacaoEmpresa.Ativa);
    }

    [Fact]
    public void CriarDemonstracao_QualquerIdade_NuncaVenceNemTemExpurgoNemAviso() // RF-NEB-02
    {
        var tenant = Demonstracao();
        var daquiAMuito = Agora.AddYears(10);

        tenant.ExpurgoPrevistoEm(TimeSpan.FromDays(30)).Should().BeNull();
        tenant.ExpurgoDevido(daquiAMuito, TimeSpan.FromDays(30)).Should().BeFalse();
        tenant.SomenteLeitura.Should().BeFalse();
    }

    [Fact]
    public void RestaurarDemonstracao_ConfiguracaoMudada_VoltaAoEstadoInicial() // F14-88
    {
        var tenant = Demonstracao();
        tenant.ConfigurarEmpresa("Outro nome", "resposta@exemplo.invalid", PoliticaSla.Sugerida);
        tenant.ConfigurarPortal("Portal novo", 30);
        tenant.DefinirLogo("logos/x.png");

        tenant.RestaurarDemonstracao("Estúdio Aurora");

        tenant.Nome.Should().Be("Estúdio Aurora");
        tenant.NomePortal.Should().BeNull();
        tenant.ResponderPara.Should().BeNull();
        tenant.LogoChave.Should().BeNull();
        tenant.SlaPadrao.Should().BeNull();
        tenant.DiasFechamentoAutomatico.Should().Be(Tenant.DiasFechamentoPadrao);
        tenant.Situacao.Should().Be(SituacaoEmpresa.Ativa);
        tenant.Demonstracao.Should().BeTrue();
        tenant.CriadoPorUsuarioId.Should().Be(Dono);
    }

    [Fact]
    public void RestaurarDemonstracao_EmpresaComum_RecusaSemMudar() // F14-88: nenhuma outra empresa é tocada
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio Traço", Agora, Dono, TimeSpan.FromDays(30));
        tenant.ConfigurarPortal("Portal do Traço", 10);

        var acao = () => tenant.RestaurarDemonstracao("Estúdio Aurora");

        acao.Should().Throw<TransicaoInvalidaException>().WithMessage("*demonstração*");
        tenant.Nome.Should().Be("Estúdio Traço");
        tenant.NomePortal.Should().Be("Portal do Traço");
    }

    private static Tenant Demonstracao() => Tenant.CriarDemonstracao(Guid.NewGuid(), "Estúdio Aurora", Agora, Dono);
}
