using Acordu.Domain.Enums;
using Acordu.Domain.Services;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.UnitTests.Services;

/// <summary>Outubro de 2026: dia 1 é quinta, 6 é terça, 12 é segunda.</summary>
public sealed class GeradorDeDatasTests
{
    private static DateOnly D(int ano, int mes, int dia) => new(ano, mes, dia);

    private static RegraRecorrencia Regra(Periodicidade periodicidade, int dia, DateOnly inicio, DateOnly? fim = null) =>
        new(periodicidade, dia, antecedenciaDias: 0, inicio, fim);

    public sealed class Mensal
    {
        [Fact]
        public void Dia31_ClampEmMesesCurtosSemContaminarOsSeguintes()
        {
            // Exemplo do contrato da API (§10, prévia).
            var regra = Regra(Periodicidade.Mensal, 31, D(2026, 10, 1));

            GeradorDeDatas.Ocorrencias(regra, D(2026, 10, 1), D(2027, 3, 31)).Should().Equal(
                D(2026, 10, 31), D(2026, 11, 30), D(2026, 12, 31), D(2027, 1, 31), D(2027, 2, 28), D(2027, 3, 31));
        }

        [Fact]
        public void Dia29_AnoBissexto()
        {
            var regra = Regra(Periodicidade.Mensal, 29, D(2028, 1, 1));

            GeradorDeDatas.Ocorrencias(regra, D(2028, 1, 1), D(2028, 3, 31)).Should().Equal(
                D(2028, 1, 29), D(2028, 2, 29), D(2028, 3, 29));
        }

        [Fact]
        public void DiaJaPassadoNoMesDoInicio_ComecaNoMesSeguinte()
        {
            var regra = Regra(Periodicidade.Mensal, 5, D(2026, 10, 6));

            GeradorDeDatas.Proxima(regra, D(2026, 10, 6)).Should().Be(D(2026, 11, 5));
        }

        [Fact]
        public void Proxima_NoProprioDia_IncluiODia()
        {
            var regra = Regra(Periodicidade.Mensal, 10, D(2026, 10, 1));

            GeradorDeDatas.Proxima(regra, D(2026, 10, 10)).Should().Be(D(2026, 10, 10));
            GeradorDeDatas.Proxima(regra, D(2026, 10, 11)).Should().Be(D(2026, 11, 10));
        }

        [Fact]
        public void Proxima_AntesDoInicio_UsaAAncora()
        {
            var regra = Regra(Periodicidade.Mensal, 10, D(2026, 12, 1));

            GeradorDeDatas.Proxima(regra, D(2026, 10, 1)).Should().Be(D(2026, 12, 10));
        }

        [Fact]
        public void ViradaDeAno()
        {
            var regra = Regra(Periodicidade.Mensal, 15, D(2026, 12, 20));

            GeradorDeDatas.Proxima(regra, D(2026, 12, 20)).Should().Be(D(2027, 1, 15));
        }
    }

    public sealed class Trimestral
    {
        [Fact]
        public void CicloAncoradoNoMesDoInicio()
        {
            var regra = Regra(Periodicidade.Trimestral, 10, D(2026, 10, 1));

            GeradorDeDatas.Ocorrencias(regra, D(2026, 10, 1), D(2027, 12, 31)).Should().Equal(
                D(2026, 10, 10), D(2027, 1, 10), D(2027, 4, 10), D(2027, 7, 10), D(2027, 10, 10));
        }

        [Fact]
        public void DiaJaPassadoNoMesDoInicio_PulaParaOProximoCiclo()
        {
            var regra = Regra(Periodicidade.Trimestral, 10, D(2026, 10, 15));

            GeradorDeDatas.Proxima(regra, D(2026, 10, 15)).Should().Be(D(2027, 1, 10));
        }

        [Fact]
        public void Proxima_NoMeioDoCiclo_VaiParaOProximoMesDoCiclo()
        {
            var regra = Regra(Periodicidade.Trimestral, 10, D(2026, 10, 1));

            GeradorDeDatas.Proxima(regra, D(2026, 11, 20)).Should().Be(D(2027, 1, 10));
        }

        [Fact]
        public void Dia31_ClampNoMesDoCiclo()
        {
            var regra = Regra(Periodicidade.Trimestral, 31, D(2026, 11, 1));

            GeradorDeDatas.Ocorrencias(regra, D(2026, 11, 1), D(2027, 8, 31)).Should().Equal(
                D(2026, 11, 30), D(2027, 2, 28), D(2027, 5, 31), D(2027, 8, 31));
        }
    }

    public sealed class Anual
    {
        [Fact]
        public void MesmoMesDoInicio_ComClampEmFevereiro()
        {
            var regra = Regra(Periodicidade.Anual, 29, D(2027, 2, 1));

            GeradorDeDatas.Ocorrencias(regra, D(2027, 1, 1), D(2029, 12, 31)).Should().Equal(
                D(2027, 2, 28), D(2028, 2, 29), D(2029, 2, 28));
        }

        [Fact]
        public void DiaJaPassado_ProximoAno()
        {
            var regra = Regra(Periodicidade.Anual, 5, D(2026, 10, 6));

            GeradorDeDatas.Proxima(regra, D(2026, 10, 6)).Should().Be(D(2027, 10, 5));
        }
    }

    public sealed class Semanal
    {
        [Theory]
        [InlineData(1, 12)]
        [InlineData(2, 6)]
        [InlineData(5, 9)]
        [InlineData(7, 11)]
        public void DiaIso_ProximoDiaDaSemana(int diaIso, int diaEsperado)
        {
            var regra = Regra(Periodicidade.Semanal, diaIso, D(2026, 10, 6));

            GeradorDeDatas.Proxima(regra, D(2026, 10, 6)).Should().Be(D(2026, 10, diaEsperado));
        }

        [Fact]
        public void Ocorrencias_TodaSemana()
        {
            var regra = Regra(Periodicidade.Semanal, 1, D(2026, 10, 1));

            GeradorDeDatas.Ocorrencias(regra, D(2026, 10, 1), D(2026, 10, 31)).Should().Equal(
                D(2026, 10, 5), D(2026, 10, 12), D(2026, 10, 19), D(2026, 10, 26));
        }
    }

    public sealed class Fim
    {
        [Fact]
        public void OcorrenciaDepoisDoFim_Nula()
        {
            var regra = Regra(Periodicidade.Mensal, 31, D(2026, 10, 1), fim: D(2026, 12, 30));

            GeradorDeDatas.Ocorrencias(regra, D(2026, 10, 1), D(2027, 12, 31)).Should().Equal(
                D(2026, 10, 31), D(2026, 11, 30));
            GeradorDeDatas.Proxima(regra, D(2026, 12, 1)).Should().BeNull();
        }

        [Fact]
        public void OcorrenciaNoDiaDoFim_Inclui()
        {
            var regra = Regra(Periodicidade.Mensal, 31, D(2026, 10, 1), fim: D(2026, 12, 31));

            GeradorDeDatas.Proxima(regra, D(2026, 12, 1)).Should().Be(D(2026, 12, 31));
        }
    }
}
