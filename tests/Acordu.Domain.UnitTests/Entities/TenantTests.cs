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

    [Fact] // F14-29: só leitura há o prazo ou mais (a borda conta)
    public void ExpurgoDevido_VencidaHaOPrazoOuMais_Verdadeiro()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora, Criador, TimeSpan.FromDays(30));
        tenant.Vencer(Agora.AddDays(30));

        tenant.ExpurgoDevido(Agora.AddDays(60), TimeSpan.FromDays(30)).Should().BeTrue();
        tenant.ExpurgoDevido(Agora.AddDays(400), TimeSpan.FromDays(30)).Should().BeTrue();
    }

    [Fact] // F14-30
    public void ExpurgoDevido_VencidaComMenosQueOPrazo_Falso()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora, Criador, TimeSpan.FromDays(30));
        tenant.Vencer(Agora.AddDays(30));

        tenant.ExpurgoDevido(Agora.AddDays(60).AddMinutes(-1), TimeSpan.FromDays(30)).Should().BeFalse();
    }

    [Fact] // F14-31: em teste, de qualquer idade
    public void ExpurgoDevido_EmTeste_Falso()
    {
        var tenant = Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora, Criador, TimeSpan.FromDays(30));

        tenant.ExpurgoDevido(Agora.AddYears(5), TimeSpan.FromDays(30)).Should().BeFalse();
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

    // Avisos de situação aos Donos (F14j, design D7). O fim do teste é Agora + 30 dias; o expurgo, 30 dias depois do
    // início do só leitura. A prorrogação e o cancelamento são do F14l: aqui o estado é posto por reflexão.

    private static readonly PrazosAvisosSituacao Prazos = new([7, 1], 7, TimeSpan.FromDays(30));
    private static readonly DateTimeOffset FimDoTeste = Agora + TimeSpan.FromDays(30);
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    private static readonly TimeZoneInfo Manaus = TimeZoneInfo.FindSystemTimeZoneById("America/Manaus");

    /// <summary>O diário de produção: 06:40 UTC (deploy-ci §5).</summary>
    private static DateTimeOffset DiarioEm(int mes, int dia) => new(2026, mes, dia, 6, 40, 0, TimeSpan.Zero);

    [Fact]
    public void AvisoDeSituacaoDevido_CadastroAsTrezeEDiarioAsSeisEQuarenta_SeteDiasSaiNoDiaCerto() // F14-39
    {
        // Cadastro 06/10 13:00 UTC: fim 05/11 13:00 UTC, último dia 05/11. O diário de 29/10 às 06:40 UTC está a mais de
        // 7 × 24 h do fim, mas é o dia de 7 dias antes no fuso.
        var tenant = EmTeste();

        tenant.AvisoDeSituacaoDevido(DiarioEm(10, 28), SaoPaulo, Prazos).Should().BeNull();
        var aviso = tenant.AvisoDeSituacaoDevido(DiarioEm(10, 29), SaoPaulo, Prazos);

        aviso!.DiasAntes.Should().Be(7);
    }

    [Fact]
    public void AvisoDeSituacaoDevido_DiarioAsSeisEQuarentaDaVespera_UmDiaSaiNaVespera() // F14-39
    {
        var tenant = EmTeste();
        tenant.RegistrarAviso(tenant.AvisoDeSituacaoDevido(DiarioEm(10, 29), SaoPaulo, Prazos)!);

        tenant.AvisoDeSituacaoDevido(DiarioEm(11, 3), SaoPaulo, Prazos).Should().BeNull();
        var aviso = tenant.AvisoDeSituacaoDevido(DiarioEm(11, 4), SaoPaulo, Prazos);

        aviso!.DiasAntes.Should().Be(1, "o diário da véspera (04/11) manda o de 1 dia, mesmo a 30 h do fim");
    }

    [Fact]
    public void AvisoDeSituacaoDevido_FusoDaEmpresa_DecideOUltimoDia() // F14-39
    {
        // Fim 06/11 03:30 UTC: último dia 05/11 em Manaus (23:29) e 06/11 em São Paulo (00:29).
        var tenant = EmTeste();
        Definir(tenant, nameof(Tenant.FimDoTeste), (DateTimeOffset?)new DateTimeOffset(2026, 11, 6, 3, 30, 0, TimeSpan.Zero));

        tenant.AvisoDeSituacaoDevido(DiarioEm(11, 4), Manaus, Prazos)!.DiasAntes.Should().Be(1);
        tenant.AvisoDeSituacaoDevido(DiarioEm(11, 4), SaoPaulo, Prazos)!.DiasAntes.Should().Be(7);
    }

    [Fact]
    public void AvisoDeSituacaoDevido_AntesDoExpurgoNoDiario_SaiNoDiaDeSeteAntesNoFuso() // F14-42
    {
        // Vencida no diário de 05/11 às 13:00 UTC: expurgo em 05/12 13:00 UTC (dia 05/12); o aviso é do dia 28/11.
        var tenant = EmTeste();
        tenant.Vencer(FimDoTeste);
        tenant.RegistrarAviso(tenant.AvisoDeSituacaoDevido(FimDoTeste, SaoPaulo, Prazos)!);

        tenant.AvisoDeSituacaoDevido(DiarioEm(11, 27), SaoPaulo, Prazos).Should().BeNull();
        tenant.AvisoDeSituacaoDevido(DiarioEm(11, 28), SaoPaulo, Prazos)!.Tipo.Should().Be(TipoAvisoSituacao.AntesDoExpurgo);
    }

    [Fact]
    public void AvisoDeSituacaoDevido_TesteNoDiaDoAvisoDeSeteDias_DevolveOAvisoDeSete() // F14-39
    {
        var tenant = EmTeste();

        var aviso = tenant.AvisoDeSituacaoDevido(FimDoTeste - TimeSpan.FromDays(7), SaoPaulo, Prazos);

        aviso.Should().Be(new AvisoSituacao(
            TipoAvisoSituacao.AntesDoFimDoTeste, FimDoTeste, 7, ["fim-do-teste-7:2026-11-05T13:00:00Z"]));
    }

    [Fact]
    public void AvisoDeSituacaoDevido_TesteForaDaJanela_Nulo() // F14-39
    {
        var tenant = EmTeste();

        // 02:00 UTC de 29/10 ainda é 28/10 em São Paulo: 8 dias antes do último dia (05/11).
        tenant.AvisoDeSituacaoDevido(FimDoTeste - TimeSpan.FromDays(7) - TimeSpan.FromHours(11), SaoPaulo, Prazos).Should().BeNull();
    }

    [Fact]
    public void AvisoDeSituacaoDevido_AvisoJaMarcado_Nulo() // F14-40
    {
        var tenant = EmTeste();
        var momento = FimDoTeste - TimeSpan.FromDays(6);
        tenant.RegistrarAviso(tenant.AvisoDeSituacaoDevido(momento, SaoPaulo, Prazos)!);

        tenant.AvisoDeSituacaoDevido(momento, SaoPaulo, Prazos).Should().BeNull();
        tenant.AvisoDeSituacaoDevido(momento + TimeSpan.FromDays(1), SaoPaulo, Prazos).Should().BeNull();
    }

    [Fact]
    public void AvisoDeSituacaoDevido_DepoisDoDeSeteChegaODeUm_DevolveODeUm() // F14-39
    {
        var tenant = EmTeste();
        tenant.RegistrarAviso(tenant.AvisoDeSituacaoDevido(FimDoTeste - TimeSpan.FromDays(7), SaoPaulo, Prazos)!);

        var aviso = tenant.AvisoDeSituacaoDevido(FimDoTeste - TimeSpan.FromHours(20), SaoPaulo, Prazos);

        aviso!.DiasAntes.Should().Be(1);
        aviso.Marcas.Should().Equal("fim-do-teste-1:2026-11-05T13:00:00Z", "fim-do-teste-7:2026-11-05T13:00:00Z");
    }

    [Fact]
    public void AvisoDeSituacaoDevido_ProrrogadoDepoisDoAviso_DevolveDeNovoComONovoFim() // F14-101
    {
        var tenant = EmTeste();
        tenant.RegistrarAviso(tenant.AvisoDeSituacaoDevido(FimDoTeste - TimeSpan.FromDays(7), SaoPaulo, Prazos)!);
        var novoFim = FimDoTeste + TimeSpan.FromDays(10);
        Definir(tenant, nameof(Tenant.FimDoTeste), (DateTimeOffset?)novoFim);

        tenant.AvisoDeSituacaoDevido(FimDoTeste - TimeSpan.FromDays(6), SaoPaulo, Prazos).Should().BeNull("o novo fim ainda está longe");
        var aviso = tenant.AvisoDeSituacaoDevido(novoFim - TimeSpan.FromDays(7), SaoPaulo, Prazos);

        aviso!.Referencia.Should().Be(novoFim);
        aviso.DiasAntes.Should().Be(7);
    }

    [Fact]
    public void RegistrarAviso_ReferenciaNova_TiraAsMarcasDoFimAntigo() // F14-101
    {
        var tenant = EmTeste();
        tenant.RegistrarAviso(tenant.AvisoDeSituacaoDevido(FimDoTeste - TimeSpan.FromDays(7), SaoPaulo, Prazos)!);
        var novoFim = FimDoTeste + TimeSpan.FromDays(10);
        Definir(tenant, nameof(Tenant.FimDoTeste), (DateTimeOffset?)novoFim);

        tenant.RegistrarAviso(tenant.AvisoDeSituacaoDevido(novoFim - TimeSpan.FromDays(7), SaoPaulo, Prazos)!);

        tenant.AvisosSituacao.Should().Equal("fim-do-teste-7:2026-11-15T13:00:00Z");
    }

    [Fact]
    public void AvisoDeSituacaoDevido_MomentoDoDeSetePassou_DevolveUmAvisoSoDoDeSete() // F14-102
    {
        var tenant = EmTeste();

        // O diário não rodou no dia (ou o teste foi prorrogado para perto): faltam 4 dias.
        var aviso = tenant.AvisoDeSituacaoDevido(FimDoTeste - TimeSpan.FromDays(4), SaoPaulo, Prazos);

        aviso!.DiasAntes.Should().Be(7);
        aviso.Marcas.Should().ContainSingle();
    }

    [Fact]
    public void AvisoDeSituacaoDevido_DoisDevidos_DevolveODeUmEMarcaOsDois() // F14-103
    {
        var tenant = EmTeste();
        var momento = FimDoTeste - TimeSpan.FromHours(12);

        var aviso = tenant.AvisoDeSituacaoDevido(momento, SaoPaulo, Prazos)!;
        tenant.RegistrarAviso(aviso);

        aviso.DiasAntes.Should().Be(1);
        aviso.Marcas.Should().HaveCount(2);
        tenant.AvisoDeSituacaoDevido(momento + TimeSpan.FromHours(1), SaoPaulo, Prazos).Should().BeNull("o de 7 dias não sai depois");
    }

    [Fact]
    public void AvisoDeSituacaoDevido_FimDoTesteJaPassou_Nulo()
    {
        var tenant = EmTeste();

        tenant.AvisoDeSituacaoDevido(FimDoTeste, SaoPaulo, Prazos).Should().BeNull();
    }

    [Fact]
    public void AvisoDeSituacaoDevido_RecemVencida_DevolveOAvisoDoVencimentoComADataDoExpurgo() // F14-41
    {
        var tenant = EmTeste();
        tenant.Vencer(FimDoTeste);

        var aviso = tenant.AvisoDeSituacaoDevido(FimDoTeste, SaoPaulo, Prazos);

        aviso.Should().Be(new AvisoSituacao(
            TipoAvisoSituacao.Vencimento, FimDoTeste + TimeSpan.FromDays(30), null, ["vencimento:2026-12-05T13:00:00Z"]));
    }

    [Fact]
    public void AvisoDeSituacaoDevido_VencidaJaAvisadaNoDiaDoAvisoDoExpurgo_DevolveODoExpurgo() // F14-42
    {
        var tenant = EmTeste();
        tenant.Vencer(FimDoTeste);
        tenant.RegistrarAviso(tenant.AvisoDeSituacaoDevido(FimDoTeste, SaoPaulo, Prazos)!);
        var expurgo = FimDoTeste + TimeSpan.FromDays(30);

        tenant.AvisoDeSituacaoDevido(expurgo - TimeSpan.FromDays(8), SaoPaulo, Prazos).Should().BeNull();
        var aviso = tenant.AvisoDeSituacaoDevido(expurgo - TimeSpan.FromDays(7), SaoPaulo, Prazos);

        aviso.Should().Be(new AvisoSituacao(TipoAvisoSituacao.AntesDoExpurgo, expurgo, 7, ["expurgo-7:2026-12-05T13:00:00Z"]));
    }

    [Fact]
    public void AvisoDeSituacaoDevido_VencidaSemAvisoDoVencimentoPertoDoExpurgo_DevolveOVencimentoEMarcaOsDois() // F14-41, F14-42
    {
        var tenant = EmTeste();
        tenant.Vencer(FimDoTeste);
        var expurgo = FimDoTeste + TimeSpan.FromDays(30);

        var aviso = tenant.AvisoDeSituacaoDevido(expurgo - TimeSpan.FromDays(3), SaoPaulo, Prazos)!;
        tenant.RegistrarAviso(aviso);

        aviso.Tipo.Should().Be(TipoAvisoSituacao.Vencimento);
        aviso.Marcas.Should().HaveCount(2);
        tenant.AvisoDeSituacaoDevido(expurgo - TimeSpan.FromDays(2), SaoPaulo, Prazos).Should().BeNull();
    }

    [Fact]
    public void AvisoDeSituacaoDevido_CanceladaNoDiaDoAvisoDoExpurgo_DevolveODoExpurgoSemOVencimento() // F14-42
    {
        var tenant = EmTeste();
        var desde = Agora + TimeSpan.FromDays(2);
        Definir(tenant, nameof(Tenant.Situacao), SituacaoEmpresa.Cancelada);
        Definir(tenant, nameof(Tenant.SomenteLeituraDesde), (DateTimeOffset?)desde);
        var expurgo = desde + TimeSpan.FromDays(30);

        tenant.AvisoDeSituacaoDevido(desde, SaoPaulo, Prazos).Should().BeNull("cancelada não tem aviso de vencimento");
        tenant.AvisoDeSituacaoDevido(expurgo - TimeSpan.FromDays(7), SaoPaulo, Prazos)!.Tipo.Should().Be(TipoAvisoSituacao.AntesDoExpurgo);
    }

    [Theory] // F14-113
    [InlineData(SituacaoEmpresa.Vencida)]
    [InlineData(SituacaoEmpresa.Cancelada)]
    public void AvisoDeSituacaoDevido_DataDoExpurgoJaPassou_Nulo(SituacaoEmpresa situacao)
    {
        var tenant = EmTeste();
        var desde = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Definir(tenant, nameof(Tenant.Situacao), situacao);
        Definir(tenant, nameof(Tenant.SomenteLeituraDesde), (DateTimeOffset?)desde);

        tenant.AvisoDeSituacaoDevido(Agora, SaoPaulo, Prazos).Should().BeNull();
        tenant.AvisoDeSituacaoDevido(desde + TimeSpan.FromDays(30), SaoPaulo, Prazos).Should().BeNull("no instante do expurgo também não");
    }

    [Fact]
    public void AvisoDeSituacaoDevido_Ativa_Nulo()
    {
        var tenant = EmTeste();
        Definir(tenant, nameof(Tenant.Situacao), SituacaoEmpresa.Ativa);

        tenant.AvisoDeSituacaoDevido(FimDoTeste - TimeSpan.FromDays(1), SaoPaulo, Prazos).Should().BeNull();
    }

    // ---------- Transições da área da Nebrys (F14l; modelo §4.11.3, design §1.2) ----------

    private static readonly DateTimeOffset Acao = Agora + TimeSpan.FromDays(40);

    /// <summary>Em TESTE vencido no fim do teste (o só leitura começa no fim).</summary>
    private static Tenant Vencida()
    {
        var tenant = EmTeste();
        tenant.Vencer(FimDoTeste);
        return tenant;
    }

    private static Tenant Ativa()
    {
        var tenant = EmTeste();
        tenant.Ativar(Agora + TimeSpan.FromDays(1));
        return tenant;
    }

    private static Tenant Cancelada()
    {
        var tenant = EmTeste();
        tenant.Cancelar(Agora + TimeSpan.FromDays(2));
        return tenant;
    }

    [Fact]
    public void Ativar_EmTeste_FicaAtivaSemFimDoTesteENaoMarcaAVolta() // F14-65
    {
        var tenant = EmTeste();

        tenant.Ativar(Acao);

        tenant.Situacao.Should().Be(SituacaoEmpresa.Ativa);
        tenant.FimDoTeste.Should().BeNull();
        tenant.SomenteLeituraDesde.Should().BeNull();
        tenant.SomenteLeitura.Should().BeFalse();
        tenant.GravavelDesde.Should().BeNull("a empresa em teste não estava só leitura");
    }

    [Fact]
    public void Ativar_Vencida_SaiDoSomenteLeituraEGravaAVolta() // F14-65, F14-24
    {
        var tenant = Vencida();

        tenant.Ativar(new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.FromHours(-3)));

        tenant.Situacao.Should().Be(SituacaoEmpresa.Ativa);
        tenant.FimDoTeste.Should().BeNull();
        tenant.SomenteLeituraDesde.Should().BeNull();
        tenant.ExpurgoPrevistoEm(TimeSpan.FromDays(30)).Should().BeNull();
        tenant.GravavelDesde.Should().Be(new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero));
        tenant.GravavelDesde!.Value.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Ativar_JaAtiva_LancaTransicaoInvalidaENaoMuda() // F14-69
    {
        var tenant = Ativa();

        var acao = () => tenant.Ativar(Acao);

        acao.Should().Throw<TransicaoInvalidaException>().WithMessage("*já está ativa*");
        tenant.Situacao.Should().Be(SituacaoEmpresa.Ativa);
        tenant.GravavelDesde.Should().BeNull();
    }

    [Fact]
    public void Ativar_Cancelada_LancaTransicaoInvalidaEApontaAReativacao() // F14-69
    {
        var tenant = Cancelada();

        var acao = () => tenant.Ativar(Acao);

        acao.Should().Throw<TransicaoInvalidaException>().WithMessage("*reativação*");
        tenant.Situacao.Should().Be(SituacaoEmpresa.Cancelada);
    }

    [Fact]
    public void Prorrogar_EmTeste_FimNoInicioDoDiaSeguinteNoFusoSemMarcarAVolta() // F14-66
    {
        var tenant = EmTeste();

        tenant.Prorrogar(new DateOnly(2026, 12, 15), SaoPaulo, Acao);

        tenant.Situacao.Should().Be(SituacaoEmpresa.Teste);
        // 16/12 00:00 em São Paulo (UTC−3).
        tenant.FimDoTeste.Should().Be(new DateTimeOffset(2026, 12, 16, 3, 0, 0, TimeSpan.Zero));
        tenant.FimDoTeste!.Value.Offset.Should().Be(TimeSpan.Zero);
        Tenant.UltimoDiaDoTeste(tenant.FimDoTeste.Value, SaoPaulo).Should().Be(new DateOnly(2026, 12, 15));
        tenant.GravavelDesde.Should().BeNull();
    }

    [Fact]
    public void Prorrogar_OutroFuso_FimNaMeiaNoiteDele() // F14-66
    {
        var tenant = EmTeste();

        tenant.Prorrogar(new DateOnly(2026, 12, 15), Manaus, Acao);

        // 16/12 00:00 em Manaus (UTC−4).
        tenant.FimDoTeste.Should().Be(new DateTimeOffset(2026, 12, 16, 4, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Prorrogar_Vencida_VoltaAoTesteForaDoSomenteLeituraEGravaAVolta() // F14-66, F14-24
    {
        var tenant = Vencida();

        tenant.Prorrogar(new DateOnly(2026, 12, 15), SaoPaulo, Acao);

        tenant.Situacao.Should().Be(SituacaoEmpresa.Teste);
        tenant.FimDoTeste.Should().Be(new DateTimeOffset(2026, 12, 16, 3, 0, 0, TimeSpan.Zero));
        tenant.SomenteLeituraDesde.Should().BeNull();
        tenant.SomenteLeitura.Should().BeFalse();
        tenant.GravavelDesde.Should().Be(Acao);
    }

    [Fact]
    public void Prorrogar_AteHoje_ValeAteOFimDoDia() // F14-66 (borda)
    {
        var tenant = Vencida();
        var hoje = Tenant.DiaCivil(Acao, SaoPaulo);

        tenant.Prorrogar(hoje, SaoPaulo, Acao);

        tenant.Situacao.Should().Be(SituacaoEmpresa.Teste);
        tenant.FimDoTeste.Should().BeAfter(Acao);
    }

    [Fact]
    public void Prorrogar_DataPassada_LancaTransicaoInvalidaENaoMuda() // F14-69
    {
        var tenant = Vencida();
        var ontem = Tenant.DiaCivil(Acao, SaoPaulo).AddDays(-1);

        var acao = () => tenant.Prorrogar(ontem, SaoPaulo, Acao);

        acao.Should().Throw<TransicaoInvalidaException>().WithMessage("*hoje ou uma data futura*");
        tenant.Situacao.Should().Be(SituacaoEmpresa.Vencida);
        tenant.FimDoTeste.Should().Be(FimDoTeste);
        tenant.SomenteLeituraDesde.Should().Be(FimDoTeste);
        tenant.GravavelDesde.Should().BeNull();
    }

    [Fact]
    public void Prorrogar_AtivaOuCancelada_LancaTransicaoInvalida() // F14-69
    {
        foreach (var tenant in new[] { Ativa(), Cancelada() })
        {
            var situacao = tenant.Situacao;

            var acao = () => tenant.Prorrogar(new DateOnly(2026, 12, 15), SaoPaulo, Acao);

            acao.Should().Throw<TransicaoInvalidaException>();
            tenant.Situacao.Should().Be(situacao);
        }
    }

    [Fact]
    public void Cancelar_EmTesteOuAtiva_SoLeituraDesdeOCancelamento() // F14-67
    {
        foreach (var tenant in new[] { EmTeste(), Ativa() })
        {
            tenant.Cancelar(Acao);

            tenant.Situacao.Should().Be(SituacaoEmpresa.Cancelada);
            tenant.SomenteLeitura.Should().BeTrue();
            tenant.CanceladaEm.Should().Be(Acao);
            tenant.SomenteLeituraDesde.Should().Be(Acao);
            tenant.ExpurgoPrevistoEm(TimeSpan.FromDays(30)).Should().Be(Acao + TimeSpan.FromDays(30));
        }
    }

    [Fact]
    public void Cancelar_Vencida_MantemOInicioDoSomenteLeituraEGravaOCancelamento() // F14-67 (design §1.2)
    {
        var tenant = Vencida();

        tenant.Cancelar(Acao);

        tenant.Situacao.Should().Be(SituacaoEmpresa.Cancelada);
        tenant.CanceladaEm.Should().Be(Acao);
        tenant.SomenteLeituraDesde.Should().Be(FimDoTeste, "o só leitura conta do vencimento");
        tenant.ExpurgoPrevistoEm(TimeSpan.FromDays(30)).Should().Be(FimDoTeste + TimeSpan.FromDays(30));
    }

    [Fact]
    public void Cancelar_JaCancelada_LancaTransicaoInvalidaENaoMudaAData() // F14-69
    {
        var tenant = Cancelada();
        var canceladaEm = tenant.CanceladaEm;

        var acao = () => tenant.Cancelar(Acao);

        acao.Should().Throw<TransicaoInvalidaException>().WithMessage("*já está cancelada*");
        tenant.CanceladaEm.Should().Be(canceladaEm);
    }

    [Fact]
    public void Reativar_Cancelada_FicaAtivaForaDoSomenteLeituraEGravaAVolta() // F14-68, F14-24
    {
        var tenant = Cancelada();

        tenant.Reativar(Acao);

        tenant.Situacao.Should().Be(SituacaoEmpresa.Ativa);
        tenant.SomenteLeitura.Should().BeFalse();
        tenant.SomenteLeituraDesde.Should().BeNull();
        tenant.CanceladaEm.Should().BeNull();
        tenant.FimDoTeste.Should().BeNull();
        tenant.ExpurgoPrevistoEm(TimeSpan.FromDays(30)).Should().BeNull();
        tenant.GravavelDesde.Should().Be(Acao);
    }

    [Fact]
    public void Reativar_ForaDeCancelada_LancaTransicaoInvalida() // F14-69
    {
        foreach (var tenant in new[] { EmTeste(), Ativa(), Vencida() })
        {
            var situacao = tenant.Situacao;

            var acao = () => tenant.Reativar(Acao);

            acao.Should().Throw<TransicaoInvalidaException>().WithMessage("*Só uma empresa cancelada*");
            tenant.Situacao.Should().Be(situacao);
        }
    }

    [Fact]
    public void Transicoes_EmpresaDeDemonstracao_TodasRecusadas() // F14-69
    {
        var tenant = EmTeste();
        Definir(tenant, nameof(Tenant.Situacao), SituacaoEmpresa.Ativa);
        Definir(tenant, nameof(Tenant.Demonstracao), true);
        Action[] acoes =
        [
            () => tenant.Ativar(Acao),
            () => tenant.Prorrogar(new DateOnly(2026, 12, 15), SaoPaulo, Acao),
            () => tenant.Cancelar(Acao),
            () => tenant.Reativar(Acao),
        ];

        foreach (var acao in acoes)
            acao.Should().Throw<TransicaoInvalidaException>().WithMessage("*demonstração*");
        tenant.Situacao.Should().Be(SituacaoEmpresa.Ativa);
        tenant.CanceladaEm.Should().BeNull();
    }

    [Fact]
    public void Criar_NaoEhDemonstracao()
    {
        EmTeste().Demonstracao.Should().BeFalse();
    }

    private static Tenant EmTeste() => Tenant.Criar(Guid.NewGuid(), "Estúdio", Agora, Criador, TimeSpan.FromDays(30));

    private static void Definir(Tenant tenant, string propriedade, object? valor) =>
        typeof(Tenant).GetProperty(propriedade)!.SetValue(tenant, valor);
}
