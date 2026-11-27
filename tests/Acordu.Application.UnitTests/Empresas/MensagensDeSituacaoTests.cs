using Acordu.Application.Empresas;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.UnitTests.Empresas;

/// <summary>Spec do F14, F14-39, F14-41, F14-42 e F14-102: os textos dos e-mails de situação aos Donos.</summary>
public sealed class MensagensDeSituacaoTests
{
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    private static readonly OpcoesFornecedor Fornecedor = new(contato: "contato@acordu.app.br");
    private static readonly DateTimeOffset Fim = new(2026, 11, 5, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Montar_AntesDoFim_TrazOFimDoTesteOsDiasEComoAtivar() // F14-39
    {
        var aviso = new AvisoSituacao(TipoAvisoSituacao.AntesDoFimDoTeste, Fim, 7, []);

        var mensagem = MensagensDeSituacao.Montar(aviso, "dono@exemplo.com", "Ana", "Estúdio Traço", SaoPaulo, Fim.AddDays(-7), Fornecedor);

        mensagem.Para.Should().Be("dono@exemplo.com");
        mensagem.Categoria.Should().Be(MensagensDeSituacao.CategoriaFimDoTeste);
        mensagem.Assunto.Should().Contain("05/11/2026");
        mensagem.Texto.Should().Contain("Olá, Ana!").And.Contain("vai até 05/11/2026 (faltam 7 dias)")
            .And.Contain("fale com a Nebrys: contato@acordu.app.br");
        mensagem.NomeRemetente.Should().BeNull("o remetente é o Acordu, não o prestador");
    }

    [Fact]
    public void Montar_AvisoDeSeteDiasAtrasado_DizOsDiasQueDeFatoFaltam() // F14-102
    {
        var aviso = new AvisoSituacao(TipoAvisoSituacao.AntesDoFimDoTeste, Fim, 7, []);

        var mensagem = MensagensDeSituacao.Montar(aviso, "dono@exemplo.com", "Ana", "Estúdio", SaoPaulo, Fim.AddDays(-4), Fornecedor);

        mensagem.Texto.Should().Contain("faltam 4 dias").And.NotContain("7 dias");
    }

    [Theory] // F14-39, F14-103: o de 1 dia antes
    [InlineData(-20, "termina amanhã")]
    [InlineData(-2, "termina hoje")]
    public void Montar_UltimosDias_DizAmanhaOuHoje(int horas, string esperado)
    {
        var aviso = new AvisoSituacao(TipoAvisoSituacao.AntesDoFimDoTeste, Fim, 1, []);

        var mensagem = MensagensDeSituacao.Montar(aviso, "dono@exemplo.com", "Ana", "Estúdio", SaoPaulo, Fim.AddHours(horas), Fornecedor);

        mensagem.Texto.Should().Contain(esperado);
    }

    [Fact]
    public void UltimoDiaDoTeste_FimNoInicioDoDiaNoFuso_EODiaAnterior() // F14-101 (prorrogado até 15/11)
    {
        var fim = new DateTimeOffset(2026, 11, 16, 3, 0, 0, TimeSpan.Zero);

        var (ultimo, dias) = MensagensDeSituacao.UltimoDiaDoTeste(fim, SaoPaulo, new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));

        ultimo.Should().Be(new DateOnly(2026, 11, 15));
        dias.Should().Be(7);
    }

    [Fact]
    public void Montar_Vencimento_DizSoLeituraADataDoExpurgoEComoAtivar() // F14-41
    {
        var aviso = new AvisoSituacao(TipoAvisoSituacao.Vencimento, Fim.AddDays(30), null, []);

        var mensagem = MensagensDeSituacao.Montar(aviso, "dono@exemplo.com", "Ana", "Estúdio Traço", SaoPaulo, Fim, Fornecedor);

        mensagem.Categoria.Should().Be(MensagensDeSituacao.CategoriaVencimento);
        mensagem.Texto.Should().Contain("só leitura").And.Contain("05/12/2026")
            .And.Contain("Para ativar a conta e manter os dados, fale com a Nebrys: contato@acordu.app.br");
    }

    [Fact]
    public void Montar_AntesDoExpurgo_TrazADataDoExpurgo() // F14-42
    {
        var aviso = new AvisoSituacao(TipoAvisoSituacao.AntesDoExpurgo, Fim.AddDays(30), 7, []);

        var mensagem = MensagensDeSituacao.Montar(aviso, "dono@exemplo.com", "Ana", "Estúdio Traço", SaoPaulo, Fim.AddDays(23), Fornecedor);

        mensagem.Categoria.Should().Be(MensagensDeSituacao.CategoriaExpurgo);
        mensagem.Assunto.Should().Contain("05/12/2026");
        mensagem.Texto.Should().Contain("A partir de 05/12/2026").And.Contain("fale com a Nebrys: contato@acordu.app.br");
    }

    [Fact]
    public void Montar_SemCanalConfigurado_DizSoFaleComOFornecedor()
    {
        var aviso = new AvisoSituacao(TipoAvisoSituacao.Vencimento, Fim.AddDays(30), null, []);

        var mensagem = MensagensDeSituacao.Montar(aviso, "dono@exemplo.com", "Ana", "Estúdio", SaoPaulo, Fim, new OpcoesFornecedor());

        mensagem.Texto.Should().Contain("fale com a Nebrys.");
    }
}
