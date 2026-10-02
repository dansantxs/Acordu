using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;
using static Acordu.Domain.UnitTests.Calendarios;

namespace Acordu.Domain.UnitTests.ValueObjects;

/// <summary>
/// Regras do §4.4 e do ADR 0003. Calendário padrão seg–sex 08–18 (Brasília); outubro de 2026: 6 é terça, 9 é sexta.
/// Prazo URGENTE: primeira resposta 1h, resolução 4h (risco a partir de 3h); ALTA: 2h e 16h.
/// </summary>
public sealed class RelogioSlaTests
{
    private static readonly PrazoSla Urgente = new(Prioridade.Urgente, Duracao.FromHoras(1), Duracao.FromHoras(4));
    private static readonly PrazoSla Alta = new(Prioridade.Alta, Duracao.FromHoras(2), Duracao.FromHoras(16));

    private static RelogioSla AbertoTercaAsDez() => RelogioSla.Iniciar(Urgente, Brasilia(6, 10), Padrao());

    public sealed class Iniciar
    {
        [Fact]
        public void DentroDoExpediente_CalculaPrazosELimiteDeRisco()
        {
            var relogio = AbertoTercaAsDez();

            relogio.AbertoEm.Should().Be(Brasilia(6, 10));
            relogio.InicioCiclo.Should().Be(Brasilia(6, 10));
            relogio.PrazoPrimeiraResposta.Should().Be(Brasilia(6, 11));
            relogio.PrazoResolucao.Should().Be(Brasilia(6, 14));
            relogio.LimiteRisco.Should().Be(Brasilia(6, 13));
            relogio.Pausado.Should().BeFalse();
            relogio.PausadoNoCiclo.Should().Be(Duracao.Zero);
        }

        [Fact]
        public void PertoDoFechamento_ViraParaODiaSeguinte()
        {
            var relogio = RelogioSla.Iniciar(Urgente, Brasilia(6, 16), Padrao());

            relogio.PrazoResolucao.Should().Be(Brasilia(7, 10));
            relogio.LimiteRisco.Should().Be(Brasilia(7, 9));
        }

        [Fact]
        public void ForaDoExpediente_ContaAPartirDaProximaAbertura()
        {
            var relogio = RelogioSla.Iniciar(Urgente, Brasilia(6, 20), Padrao());

            relogio.PrazoPrimeiraResposta.Should().Be(Brasilia(7, 9));
            relogio.PrazoResolucao.Should().Be(Brasilia(7, 12));
        }

        [Fact]
        public void InstantesSaoGuardadosEmUtc()
        {
            AbertoTercaAsDez().PrazoResolucao.Offset.Should().Be(TimeSpan.Zero);
        }

        [Fact]
        public void LimiteDeRisco_ArredondaParaCima()
        {
            // 75% de 10 minutos = 7,5 → 8 minutos.
            var prazo = new PrazoSla(Prioridade.Baixa, Duracao.FromMinutos(5), Duracao.FromMinutos(10));

            var relogio = RelogioSla.Iniciar(prazo, Brasilia(6, 10), Padrao());

            relogio.LimiteRisco.Should().Be(Brasilia(6, 10, 8));
        }
    }

    public sealed class PausarERetomar
    {
        [Fact]
        public void Pausar_GuardaOInstanteSemMudarPrazos()
        {
            var relogio = AbertoTercaAsDez().Pausar(Brasilia(6, 11));

            relogio.Pausado.Should().BeTrue();
            relogio.PausadoDesde.Should().Be(Brasilia(6, 11));
            relogio.PrazoResolucao.Should().Be(Brasilia(6, 14));
        }

        [Fact]
        public void Pausar_JaPausado_MantemOInicioDaPausa()
        {
            var relogio = AbertoTercaAsDez().Pausar(Brasilia(6, 11)).Pausar(Brasilia(6, 12));

            relogio.PausadoDesde.Should().Be(Brasilia(6, 11));
        }

        [Fact]
        public void Retomar_NoMesmoDia_EmpurraPrazoELimitePeloTempoPausado()
        {
            var relogio = AbertoTercaAsDez().Pausar(Brasilia(6, 11)).Retomar(Brasilia(6, 13), Padrao());

            relogio.Pausado.Should().BeFalse();
            relogio.PausadoNoCiclo.Should().Be(Duracao.FromHoras(2));
            relogio.PrazoResolucao.Should().Be(Brasilia(6, 16));
            relogio.LimiteRisco.Should().Be(Brasilia(6, 15));
        }

        [Fact]
        public void Retomar_NoDiaSeguinte_ContaSoHorasUteisPausadas()
        {
            // Restavam 3h úteis na pausa (11:00); retomada quarta 09:00 → vence quarta 12:00.
            var relogio = AbertoTercaAsDez().Pausar(Brasilia(6, 11)).Retomar(Brasilia(7, 9), Padrao());

            relogio.PausadoNoCiclo.Should().Be(Duracao.FromHoras(8));
            relogio.PrazoResolucao.Should().Be(Brasilia(7, 12));
        }

        [Fact]
        public void Retomar_DepoisDoFimDeSemana_IgnoraSabadoEDomingo()
        {
            var relogio = RelogioSla.Iniciar(Urgente, Brasilia(9, 16), Padrao());
            relogio.PrazoResolucao.Should().Be(Brasilia(12, 10));

            relogio = relogio.Pausar(Brasilia(9, 17)).Retomar(Brasilia(12, 9), Padrao());

            relogio.PausadoNoCiclo.Should().Be(Duracao.FromHoras(2));
            relogio.PrazoResolucao.Should().Be(Brasilia(12, 12));
        }

        [Fact]
        public void Retomar_DuasPausas_AcumulaNoCiclo()
        {
            var relogio = AbertoTercaAsDez()
                .Pausar(Brasilia(6, 11)).Retomar(Brasilia(6, 12), Padrao())
                .Pausar(Brasilia(6, 13)).Retomar(Brasilia(6, 14), Padrao());

            relogio.PausadoNoCiclo.Should().Be(Duracao.FromHoras(2));
            relogio.PrazoResolucao.Should().Be(Brasilia(6, 16));
        }

        [Fact]
        public void Retomar_SemPausa_NaoMudaNada()
        {
            var relogio = AbertoTercaAsDez();

            relogio.Retomar(Brasilia(6, 12), Padrao()).Should().Be(relogio);
        }

        [Fact]
        public void Retomar_PausaIniciadaDepoisDoPrazoVencido_NaoDeslocaOPrazo()
        {
            var relogio = AbertoTercaAsDez().Pausar(Brasilia(6, 15)).Retomar(Brasilia(6, 17), Padrao());

            relogio.PrazoResolucao.Should().Be(Brasilia(6, 14));
        }

        [Fact]
        public void Pausar_NaoMudaPrazoDePrimeiraResposta()
        {
            var relogio = AbertoTercaAsDez().Pausar(Brasilia(6, 10, 30)).Retomar(Brasilia(6, 15), Padrao());

            relogio.PrazoPrimeiraResposta.Should().Be(Brasilia(6, 11));
        }
    }

    public sealed class Repriorizar
    {
        [Fact]
        public void SemPrimeiraResposta_RecalculaAmbosOsPrazosDesdeAAbertura()
        {
            var relogio = AbertoTercaAsDez().Repriorizar(Alta, primeiraRespostaRegistrada: false, Padrao());

            relogio.PrazoPrimeiraResposta.Should().Be(Brasilia(6, 12));
            relogio.PrazoResolucao.Should().Be(Brasilia(7, 16));
            relogio.LimiteRisco.Should().Be(Brasilia(7, 12));
            relogio.TempoResolucao.Should().Be(Duracao.FromHoras(16));
        }

        [Fact]
        public void ComPrimeiraResposta_MantemOPrazoDePrimeiraResposta()
        {
            var relogio = AbertoTercaAsDez().Repriorizar(Alta, primeiraRespostaRegistrada: true, Padrao());

            relogio.PrazoPrimeiraResposta.Should().Be(Brasilia(6, 11));
            relogio.TempoPrimeiraResposta.Should().Be(Duracao.FromHoras(1));
        }

        [Fact]
        public void DepoisDeUmaPausa_DescontaOTempoPausado()
        {
            var relogio = AbertoTercaAsDez()
                .Pausar(Brasilia(6, 11)).Retomar(Brasilia(6, 13), Padrao())
                .Repriorizar(Alta, primeiraRespostaRegistrada: true, Padrao());

            // 16h + 2h pausadas = 18h úteis desde terça 10:00 → terça 8h + quarta 10h.
            relogio.PrazoResolucao.Should().Be(Brasilia(7, 18));
        }

        [Fact]
        public void ParaPrioridadeMaisAlta_PodeDeixarOPrazoNoPassado()
        {
            var relogio = RelogioSla.Iniciar(Alta, Brasilia(6, 10), Padrao())
                .Repriorizar(Urgente, primeiraRespostaRegistrada: true, Padrao());

            relogio.PrazoResolucao.Should().Be(Brasilia(6, 14));
            relogio.SituacaoResolucao(Brasilia(6, 15), Padrao()).Should().Be(SituacaoSla.Violado);
        }
    }

    public sealed class Reabrir
    {
        [Fact]
        public void NovoCicloComPrazoCheioAPartirDaReabertura()
        {
            var relogio = AbertoTercaAsDez()
                .Pausar(Brasilia(6, 11)).Retomar(Brasilia(6, 12), Padrao())
                .Reabrir(Brasilia(8, 10), Padrao());

            relogio.InicioCiclo.Should().Be(Brasilia(8, 10));
            relogio.PausadoNoCiclo.Should().Be(Duracao.Zero);
            relogio.PrazoResolucao.Should().Be(Brasilia(8, 14));
            relogio.LimiteRisco.Should().Be(Brasilia(8, 13));
        }

        [Fact]
        public void MantemAberturaEPrazoDePrimeiraResposta()
        {
            var relogio = AbertoTercaAsDez().Reabrir(Brasilia(8, 10), Padrao());

            relogio.AbertoEm.Should().Be(Brasilia(6, 10));
            relogio.PrazoPrimeiraResposta.Should().Be(Brasilia(6, 11));
        }
    }

    public sealed class SituacaoResolucao
    {
        [Theory]
        [InlineData(10, 0, SituacaoSla.NoPrazo)]
        [InlineData(12, 0, SituacaoSla.NoPrazo)]
        [InlineData(12, 59, SituacaoSla.NoPrazo)]
        [InlineData(13, 0, SituacaoSla.EmRisco)]
        [InlineData(14, 0, SituacaoSla.EmRisco)]
        [InlineData(14, 1, SituacaoSla.Violado)]
        public void SemPausa_SegueOsLimites(int hora, int minuto, SituacaoSla esperada)
        {
            AbertoTercaAsDez().SituacaoResolucao(Brasilia(6, hora, minuto), Padrao()).Should().Be(esperada);
        }

        [Fact]
        public void Pausado_FicaCongeladoNoInstanteDaPausa()
        {
            var relogio = AbertoTercaAsDez().Pausar(Brasilia(6, 12));

            relogio.SituacaoResolucao(Brasilia(9, 17), Padrao()).Should().Be(SituacaoSla.NoPrazo);
            relogio.PercentualResolucao(Brasilia(9, 17), Padrao()).Should().Be(50);
        }

        [Fact]
        public void PausadoEmRisco_ContinuaEmRisco()
        {
            var relogio = AbertoTercaAsDez().Pausar(Brasilia(6, 13, 30));

            relogio.SituacaoResolucao(Brasilia(7, 17), Padrao()).Should().Be(SituacaoSla.EmRisco);
        }

        [Fact]
        public void Percentual_DescontaOTempoPausado()
        {
            var relogio = AbertoTercaAsDez().Pausar(Brasilia(6, 11)).Retomar(Brasilia(6, 13), Padrao());

            relogio.PercentualResolucao(Brasilia(6, 14), Padrao()).Should().Be(50);
        }

        [Fact]
        public void Percentual_Violado_PassaDeCem()
        {
            AbertoTercaAsDez().PercentualResolucao(Brasilia(6, 16), Padrao()).Should().Be(150);
        }

        [Fact]
        public void LimiteDeRisco_CoincideComOPercentual()
        {
            var relogio = RelogioSla.Iniciar(Alta, Brasilia(6, 10), Padrao())
                .Pausar(Brasilia(6, 15)).Retomar(Brasilia(7, 9), Padrao());

            relogio.SituacaoResolucao(relogio.LimiteRisco.AddMinutes(-1), Padrao()).Should().Be(SituacaoSla.NoPrazo);
            relogio.SituacaoResolucao(relogio.LimiteRisco, Padrao()).Should().Be(SituacaoSla.EmRisco);
        }
    }

    public sealed class SituacaoPrimeiraResposta
    {
        [Theory]
        [InlineData(10, 30, SituacaoSla.NoPrazo)]
        [InlineData(10, 45, SituacaoSla.EmRisco)]
        [InlineData(11, 0, SituacaoSla.EmRisco)]
        [InlineData(11, 1, SituacaoSla.Violado)]
        public void SemResposta_SegueOsLimites(int hora, int minuto, SituacaoSla esperada)
        {
            AbertoTercaAsDez().SituacaoPrimeiraResposta(null, Brasilia(6, hora, minuto), Padrao()).Should().Be(esperada);
        }

        [Fact]
        public void RespondidaNoPrazo_Cumprido()
        {
            AbertoTercaAsDez().SituacaoPrimeiraResposta(Brasilia(6, 10, 50), Brasilia(6, 15), Padrao())
                .Should().Be(SituacaoSla.Cumprido);
        }

        [Fact]
        public void RespondidaAtrasada_Violado()
        {
            AbertoTercaAsDez().SituacaoPrimeiraResposta(Brasilia(6, 11, 10), Brasilia(6, 15), Padrao())
                .Should().Be(SituacaoSla.Violado);
        }

        [Fact]
        public void SemResposta_NaoPausaEmAguardandoCliente()
        {
            var relogio = AbertoTercaAsDez().Pausar(Brasilia(6, 10, 15));

            relogio.SituacaoPrimeiraResposta(null, Brasilia(6, 12), Padrao()).Should().Be(SituacaoSla.Violado);
        }
    }
}
