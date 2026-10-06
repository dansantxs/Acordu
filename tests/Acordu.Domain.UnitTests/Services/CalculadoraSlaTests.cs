using Acordu.Domain.Entities;
using Acordu.Domain.Services;
using Acordu.Domain.ValueObjects;
using static Acordu.Domain.UnitTests.Calendarios;

namespace Acordu.Domain.UnitTests.Services;

/// <summary>
/// Casos de borda do ADR 0003. Calendário padrão: seg–sex 08:00–18:00, America/Sao_Paulo.
/// Outubro de 2026: dia 6 é terça, 9 é sexta, 10 é sábado, 12 é segunda (feriado nos testes que dizem isso).
/// </summary>
public sealed class CalculadoraSlaTests
{
    public sealed class Somar
    {
        [Fact]
        public void DentroDoExpediente_SomaNoMesmoDia()
        {
            var prazo = CalculadoraSla.Somar(Padrao(), Brasilia(6, 10), Duracao.FromHoras(4));

            prazo.Should().Be(Brasilia(6, 14));
        }

        [Fact]
        public void TerminaExatamenteNoFechamento_VenceNoFechamento()
        {
            var prazo = CalculadoraSla.Somar(Padrao(), Brasilia(6, 17), Duracao.FromHoras(1));

            prazo.Should().Be(Brasilia(6, 18));
        }

        [Fact]
        public void PassaDoFechamento_ContinuaNaProximaAbertura()
        {
            var prazo = CalculadoraSla.Somar(Padrao(), Brasilia(6, 17), Duracao.FromHoras(2));

            prazo.Should().Be(Brasilia(7, 9));
        }

        [Fact]
        public void InicioAntesDoExpediente_ComecaNaAbertura()
        {
            var prazo = CalculadoraSla.Somar(Padrao(), Brasilia(6, 6, 30), Duracao.FromHoras(1));

            prazo.Should().Be(Brasilia(6, 9));
        }

        [Fact]
        public void InicioDepoisDoExpediente_ComecaNaAberturaDoDiaSeguinte()
        {
            var prazo = CalculadoraSla.Somar(Padrao(), Brasilia(6, 19), Duracao.FromHoras(1));

            prazo.Should().Be(Brasilia(7, 9));
        }

        [Fact]
        public void InicioNoSabado_ComecaNaSegunda()
        {
            var prazo = CalculadoraSla.Somar(Padrao(), Brasilia(10, 11), Duracao.FromHoras(1));

            prazo.Should().Be(Brasilia(12, 9));
        }

        [Fact]
        public void SextaAoFimDoDia_AtravessaOFimDeSemana()
        {
            var prazo = CalculadoraSla.Somar(Padrao(), Brasilia(9, 17), Duracao.FromHoras(8));

            prazo.Should().Be(Brasilia(12, 15));
        }

        [Fact]
        public void SextaAoFimDoDia_ComFeriadoNaSegunda_PulaParaTerca()
        {
            var prazo = CalculadoraSla.Somar(ComFeriadoDoze(), Brasilia(9, 17), Duracao.FromHoras(8));

            prazo.Should().Be(Brasilia(13, 15));
        }

        [Fact]
        public void VariosDias_ContaSoAsHorasUteis()
        {
            // 40h úteis a partir de terça 08:00 = 4 dias cheios → vence sexta 18:00.
            var prazo = CalculadoraSla.Somar(Padrao(), Brasilia(6, 8), Duracao.FromHoras(40));

            prazo.Should().Be(Brasilia(9, 18));
        }

        [Fact]
        public void DuracaoZero_DevolveOProprioInicio()
        {
            var prazo = CalculadoraSla.Somar(Padrao(), Brasilia(10, 11), Duracao.Zero);

            prazo.Should().Be(Brasilia(10, 11));
        }

        [Fact]
        public void DevolveSempreEmUtc()
        {
            var prazo = CalculadoraSla.Somar(Padrao(), Brasilia(6, 10), Duracao.FromHoras(1));

            prazo.Offset.Should().Be(TimeSpan.Zero);
        }

        [Fact]
        public void InicioInformadoEmOutroFuso_UsaOMesmoInstante()
        {
            var emUtc = new DateTimeOffset(2026, 10, 6, 13, 0, 0, TimeSpan.Zero); // = terça 10:00 em Brasília

            var prazo = CalculadoraSla.Somar(Padrao(), emUtc, Duracao.FromHoras(4));

            prazo.Should().Be(Brasilia(6, 14));
        }

        [Fact]
        public void ExpedienteDiferentePorDia_RespeitaCadaJanela()
        {
            var calendario = CalendarioUtil.Criar(
                Calendarios.Tenant,
                CalendarioUtil.FusoPadrao,
                [
                    new JanelaDiaria(DayOfWeek.Thursday, new TimeOnly(8, 0), new TimeOnly(18, 0)),
                    new JanelaDiaria(DayOfWeek.Friday, new TimeOnly(8, 0), new TimeOnly(12, 0)),
                ]);

            // Quinta 16:00 + 4h: 2h na quinta, 2h na sexta (que fecha ao meio-dia) → sexta 10:00.
            var prazo = CalculadoraSla.Somar(calendario, Brasilia(8, 16), Duracao.FromHoras(4));

            prazo.Should().Be(Brasilia(9, 10));
        }

        [Fact]
        public void CalendarioEmOutroFuso_CalculaNaHoraLocalDoTenant()
        {
            // Manaus é UTC-4: o expediente 08–18 de lá equivale a 09–19 em Brasília.
            var calendario = Padrao();
            calendario.AlterarFuso("America/Manaus");

            var prazo = CalculadoraSla.Somar(calendario, Brasilia(6, 18), Duracao.FromHoras(1));

            prazo.Should().Be(Brasilia(6, 19));
        }
    }

    public sealed class MinutosUteisEntre
    {
        [Fact]
        public void UmDiaCorrido_DezHorasUteis()
        {
            CalculadoraSla.MinutosUteisEntre(Padrao(), Brasilia(6, 10), Brasilia(7, 10))
                .Should().Be(Duracao.FromHoras(10));
        }

        [Fact]
        public void AtravessandoFimDeSemana_ContaSoSextaESegunda()
        {
            CalculadoraSla.MinutosUteisEntre(Padrao(), Brasilia(9, 17), Brasilia(12, 9))
                .Should().Be(Duracao.FromHoras(2));
        }

        [Fact]
        public void FeriadoNoMeio_NaoConta()
        {
            CalculadoraSla.MinutosUteisEntre(ComFeriadoDoze(), Brasilia(9, 17), Brasilia(13, 9))
                .Should().Be(Duracao.FromHoras(2));
        }

        [Fact]
        public void IntervaloTodoForaDoExpediente_EhZero()
        {
            CalculadoraSla.MinutosUteisEntre(Padrao(), Brasilia(10, 10), Brasilia(11, 20))
                .Should().Be(Duracao.Zero);
        }

        [Fact]
        public void FimAntesDoInicio_EhZero()
        {
            CalculadoraSla.MinutosUteisEntre(Padrao(), Brasilia(12, 9), Brasilia(9, 17))
                .Should().Be(Duracao.Zero);
        }

        [Fact]
        public void ArredondaParaBaixo()
        {
            var de = Brasilia(6, 10);
            var ate = de.AddMinutes(10).AddSeconds(59);

            CalculadoraSla.MinutosUteisEntre(Padrao(), de, ate).Should().Be(Duracao.FromMinutos(10));
        }

        [Theory]
        [InlineData(6, 10, 0, 1)]
        [InlineData(6, 10, 0, 600)]
        [InlineData(9, 17, 30, 601)]
        [InlineData(10, 3, 0, 59)]
        [InlineData(8, 7, 59, 3000)]
        public void EhOInversoDeSomar(int dia, int hora, int minuto, int minutosUteis)
        {
            var calendario = ComFeriadoDoze();
            var inicio = Brasilia(dia, hora, minuto);

            var prazo = CalculadoraSla.Somar(calendario, inicio, Duracao.FromMinutos(minutosUteis));

            CalculadoraSla.MinutosUteisEntre(calendario, inicio, prazo).Should().Be(Duracao.FromMinutos(minutosUteis));
        }
    }
}
