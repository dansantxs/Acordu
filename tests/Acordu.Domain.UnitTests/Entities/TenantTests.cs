using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.UnitTests.Entities;

public sealed class TenantTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 6, 13, 0, 0, TimeSpan.Zero);
    private static readonly Guid Criador = Guid.NewGuid();

    [Fact]
    public void Criar_NomeAparadoEDataInformada()
    {
        var id = Guid.NewGuid();

        var tenant = Tenant.Criar(id, "  Estúdio Traço ", Agora, Criador, TimeSpan.FromDays(30));

        tenant.Id.Should().Be(id);
        tenant.Nome.Should().Be("Estúdio Traço");
        tenant.CriadoEm.Should().Be(Agora);
    }

    [Fact]
    public void Criar_InstanteGuardadoEmUtc()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.FromHours(-3)), Criador, TimeSpan.FromDays(30));

        tenant.CriadoEm.Offset.Should().Be(TimeSpan.Zero);
        tenant.CriadoEm.Should().Be(Agora);
    }

    [Fact]
    public void Criar_IdVazio_LancaExcecao()
    {
        var acao = () => Tenant.Criar(Guid.Empty, "Estúdio", Agora, Criador, TimeSpan.FromDays(30));

        acao.Should().Throw<ArgumentException>().WithParameterName("id");
    }

    [Fact]
    public void Renomear_AcimaDoLimite_MantemONome()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora, Criador, TimeSpan.FromDays(30));

        var acao = () => tenant.Renomear(new string('x', LimitesTexto.Nome + 1));

        acao.Should().Throw<ArgumentException>().WithParameterName("nome");
        tenant.Nome.Should().Be("Estúdio");
    }

    [Fact]
    public void Criar_FechamentoAutomaticoPadraoSemNomeDePortal()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora, Criador, TimeSpan.FromDays(30));

        tenant.NomePortal.Should().BeNull();
        tenant.DiasFechamentoAutomatico.Should().Be(Tenant.DiasFechamentoPadrao);
    }

    [Fact]
    public void ConfigurarPortal_AparaNomeEGuardaDias()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora, Criador, TimeSpan.FromDays(30));

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
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora, Criador, TimeSpan.FromDays(30));

        var acao = () => tenant.ConfigurarPortal(null, dias);

        acao.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("diasFechamentoAutomatico");
        tenant.DiasFechamentoAutomatico.Should().Be(Tenant.DiasFechamentoPadrao);
    }

    [Fact]
    public void DefinirLogo_TrocaERemove()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", DateTimeOffset.UnixEpoch, Criador, TimeSpan.FromDays(30));

        tenant.DefinirLogo("t/portal/logo-1.png");
        tenant.LogoChave.Should().Be("t/portal/logo-1.png");
        tenant.DefinirLogo(null);
        tenant.LogoChave.Should().BeNull();
    }

    [Fact]
    public void Criar_SemSlaProprio_SugereODoSistema()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora, Criador, TimeSpan.FromDays(30));

        tenant.SlaPadrao.Should().BeNull();
        tenant.SlaSugerido.Should().Be(PoliticaSla.Sugerida);
        tenant.SlaSugerido.PrazosDe(Prioridade.Urgente).Resolucao.Should().Be(Duracao.FromHoras(4));
        tenant.ResponderPara.Should().BeNull();
    }

    [Fact]
    public void ConfigurarEmpresa_NormalizaEmailEGuardaSla()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora, Criador, TimeSpan.FromDays(30));
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
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora, Criador, TimeSpan.FromDays(30));

        var acao = () => tenant.ConfigurarEmpresa("Outro nome", "sem-arroba", PoliticaSla.Sugerida);

        acao.Should().Throw<ArgumentException>().WithParameterName("responderPara");
        tenant.Nome.Should().Be("Estúdio");
        tenant.SlaPadrao.Should().BeNull();
    }

    [Fact]
    public void Criar_GuardaOCriador_ENasceSemEnvioLiberado()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora, Criador, TimeSpan.FromDays(30));

        tenant.CriadoPorUsuarioId.Should().Be(Criador);
        tenant.EnvioLiberado.Should().BeFalse();
        tenant.EnvioLiberadoEm.Should().BeNull();
    }

    [Fact]
    public void Criar_SemCriador_Recusa()
    {
        var acao = () => Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora, Guid.Empty, TimeSpan.FromDays(30));

        acao.Should().Throw<ArgumentException>().WithParameterName("criadoPorUsuarioId");
    }

    // Situação da empresa (spec do F14, F14a; modelo §4.11.3).

    [Fact]
    public void Criar_NasceEmTesteComFimNoCadastroMaisADuracao() // F14-01
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora, Criador, TimeSpan.FromDays(30));

        tenant.Situacao.Should().Be(SituacaoEmpresa.Teste);
        tenant.FimDoTeste.Should().Be(Agora.AddDays(30));
        tenant.FimDoTeste!.Value.Offset.Should().Be(TimeSpan.Zero);
        tenant.SomenteLeitura.Should().BeFalse();
        tenant.SomenteLeituraDesde.Should().BeNull();
        tenant.CanceladaEm.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Criar_DuracaoDoTesteNaoPositiva_LancaExcecao(int dias)
    {
        var acao = () => Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora, Criador, TimeSpan.FromDays(dias));

        acao.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("duracaoDoTeste");
    }

    [Fact]
    public void Vencer_FimDoTesteAlcancado_FicaVencidaComADataDoVencimento() // F14-03
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora, Criador, TimeSpan.FromDays(30));
        var vencimento = Agora.AddDays(30).AddHours(3);

        tenant.Vencer(vencimento);

        tenant.Situacao.Should().Be(SituacaoEmpresa.Vencida);
        tenant.SomenteLeitura.Should().BeTrue();
        tenant.SomenteLeituraDesde.Should().Be(vencimento);
        tenant.FimDoTeste.Should().Be(Agora.AddDays(30), "o fim do teste fica como estava");
        tenant.CanceladaEm.Should().BeNull();
    }

    [Fact]
    public void Vencer_NoInstanteExatoDoFim_Vence()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora, Criador, TimeSpan.FromDays(30));

        tenant.Vencer(Agora.AddDays(30));

        tenant.Situacao.Should().Be(SituacaoEmpresa.Vencida);
    }

    [Fact]
    public void Vencer_FimDoTesteAindaPorVir_LancaTransicaoInvalidaEContinuaEmTeste() // F14-04
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora, Criador, TimeSpan.FromDays(30));

        var acao = () => tenant.Vencer(Agora.AddDays(30).AddMinutes(-1));

        acao.Should().Throw<TransicaoInvalidaException>();
        tenant.Situacao.Should().Be(SituacaoEmpresa.Teste);
        tenant.SomenteLeituraDesde.Should().BeNull();
    }

    [Fact]
    public void Vencer_JaVencida_LancaTransicaoInvalidaENaoMudaAData() // F14-05
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora, Criador, TimeSpan.FromDays(30));
        var vencimento = Agora.AddDays(31);
        tenant.Vencer(vencimento);

        var acao = () => tenant.Vencer(vencimento.AddDays(1));

        acao.Should().Throw<TransicaoInvalidaException>();
        tenant.SomenteLeituraDesde.Should().Be(vencimento);
    }

    [Fact]
    public void Vencer_InstanteComFuso_GuardaEmUtc()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora, Criador, TimeSpan.FromDays(1));
        var local = new DateTimeOffset(2026, 10, 8, 9, 0, 0, TimeSpan.FromHours(-3));

        tenant.Vencer(local);

        tenant.SomenteLeituraDesde!.Value.Offset.Should().Be(TimeSpan.Zero);
        tenant.SomenteLeituraDesde.Should().Be(local);
    }

    [Fact]
    public void ExpurgoPrevistoEm_Vencida_EhOInicioDoSoLeituraMaisOPrazo()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora, Criador, TimeSpan.FromDays(30));
        tenant.Vencer(Agora.AddDays(30));

        tenant.ExpurgoPrevistoEm(TimeSpan.FromDays(30)).Should().Be(Agora.AddDays(60));
    }

    [Fact]
    public void ExpurgoPrevistoEm_EmTeste_Nulo()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora, Criador, TimeSpan.FromDays(30));

        tenant.ExpurgoPrevistoEm(TimeSpan.FromDays(30)).Should().BeNull();
    }

    [Theory] // F14-56, F14-58: o documento é normalizado na empresa
    [InlineData("529.982.247-25", "52998224725")]
    [InlineData("11.222.333/0001-81", "11222333000181")]
    [InlineData("12.abc.345/01de-35", "12ABC34501DE35")]
    [InlineData(" 12ABC34501DE35 ", "12ABC34501DE35")]
    public void Criar_ComDocumento_GuardaNormalizado(string documento, string esperado)
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora, Criador, TimeSpan.FromDays(30), documento);

        tenant.Documento.Should().Be(esperado);
    }

    [Theory] // F14-56
    [InlineData("529.982.247-24")]
    [InlineData("11222333000182")]
    [InlineData("12ABC34501DE36")]
    [InlineData("111.111.111-11")]
    [InlineData("1234")]
    public void Criar_DocumentoInvalido_LancaExcecaoNoCampo(string digitado)
    {
        var acao = () => Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora, Criador, TimeSpan.FromDays(30), digitado);

        acao.Should().Throw<ArgumentException>().WithParameterName("documento");
    }

    [Fact]
    public void Criar_SemDocumento_FicaNulo()
    {
        // As empresas anteriores ao F14g não têm documento; quem exige no cadastro é a Application.
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora, Criador, TimeSpan.FromDays(30));

        tenant.Documento.Should().BeNull();
    }
}
