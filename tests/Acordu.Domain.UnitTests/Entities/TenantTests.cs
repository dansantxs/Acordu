using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.UnitTests.Entities;

public sealed class TenantTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 6, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Criar_NomeAparadoEDataInformada()
    {
        var id = Guid.NewGuid();

        var tenant = Tenant.Criar(id, "  Estúdio Traço ", Agora);

        tenant.Id.Should().Be(id);
        tenant.Nome.Should().Be("Estúdio Traço");
        tenant.CriadoEm.Should().Be(Agora);
    }

    [Fact]
    public void Criar_InstanteGuardadoEmUtc()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.FromHours(-3)));

        tenant.CriadoEm.Offset.Should().Be(TimeSpan.Zero);
        tenant.CriadoEm.Should().Be(Agora);
    }

    [Fact]
    public void Criar_IdVazio_LancaExcecao()
    {
        var acao = () => Tenant.Criar(Guid.Empty, "Estúdio", Agora);

        acao.Should().Throw<ArgumentException>().WithParameterName("id");
    }

    [Fact]
    public void Renomear_AcimaDoLimite_MantemONome()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora);

        var acao = () => tenant.Renomear(new string('x', LimitesTexto.Nome + 1));

        acao.Should().Throw<ArgumentException>().WithParameterName("nome");
        tenant.Nome.Should().Be("Estúdio");
    }

    [Fact]
    public void Criar_FechamentoAutomaticoPadraoSemNomeDePortal()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora);

        tenant.NomePortal.Should().BeNull();
        tenant.DiasFechamentoAutomatico.Should().Be(Tenant.DiasFechamentoPadrao);
    }

    [Fact]
    public void ConfigurarPortal_AparaNomeEGuardaDias()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora);

        tenant.ConfigurarPortal("  Estúdio Traço Design ", 10);
        tenant.ConfigurarPortal("   ", 10);

        tenant.NomePortal.Should().BeNull();
        tenant.DiasFechamentoAutomatico.Should().Be(10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void ConfigurarPortal_DiasForaDaFaixa_LancaExcecao(int dias)
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora);

        var acao = () => tenant.ConfigurarPortal(null, dias);

        acao.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("diasFechamentoAutomatico");
        tenant.DiasFechamentoAutomatico.Should().Be(Tenant.DiasFechamentoPadrao);
    }

    [Fact]
    public void DefinirLogo_TrocaERemove()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", DateTimeOffset.UnixEpoch);

        tenant.DefinirLogo("t/portal/logo-1.png");
        tenant.LogoChave.Should().Be("t/portal/logo-1.png");
        tenant.DefinirLogo(null);
        tenant.LogoChave.Should().BeNull();
    }

    [Fact]
    public void Criar_SemSlaProprio_SugereODoSistema()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora);

        tenant.SlaPadrao.Should().BeNull();
        tenant.SlaSugerido.Should().Be(PoliticaSla.Sugerida);
        tenant.SlaSugerido.PrazosDe(Prioridade.Urgente).Resolucao.Should().Be(Duracao.FromHoras(4));
        tenant.ResponderPara.Should().BeNull();
    }

    [Fact]
    public void ConfigurarEmpresa_NormalizaEmailEGuardaSla()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora);
        var sla = new PoliticaSla(Enum.GetValues<Prioridade>()
            .Select(p => new PrazoSla(p, Duracao.FromHoras(1), Duracao.FromHoras(8))));

        tenant.ConfigurarEmpresa("  Estúdio Traço ", " Contato@EstudioTraco.com.br ", sla);

        tenant.Nome.Should().Be("Estúdio Traço");
        tenant.ResponderPara.Should().Be("contato@estudiotraco.com.br");
        tenant.SlaSugerido.Should().Be(sla);

        tenant.ConfigurarEmpresa("Estúdio Traço", "   ", sla);
        tenant.ResponderPara.Should().BeNull();
    }

    [Fact]
    public void ConfigurarEmpresa_EmailInvalido_NaoMudaNada()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora);

        var acao = () => tenant.ConfigurarEmpresa("Outro nome", "sem-arroba", PoliticaSla.Sugerida);

        acao.Should().Throw<ArgumentException>().WithParameterName("responderPara");
        tenant.Nome.Should().Be("Estúdio");
        tenant.SlaPadrao.Should().BeNull();
    }
}
