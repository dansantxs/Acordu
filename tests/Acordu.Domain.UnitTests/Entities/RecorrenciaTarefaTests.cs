using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.UnitTests.Entities;

public sealed class RecorrenciaTarefaTests
{
    private static DateOnly D(int ano, int mes, int dia) => new(ano, mes, dia);

    private static ModeloTarefa Modelo() =>
        new("Fechamento mensal", "Rotina do mês", null, ["Conciliar extratos", "Emitir guias"], Duracao.FromHoras(4), true);

    /// <summary>Mensal, dia 31, 3 dias de antecedência, a partir de 01/10/2026 (exemplo do contrato da API).</summary>
    private static RecorrenciaTarefa Criar(DateOnly? fim = null, int antecedencia = 3) =>
        RecorrenciaTarefa.Criar(
            Guid.NewGuid(), Calendarios.Tenant, Contratos.Cliente, contratoId: null, Modelo(),
            new RegraRecorrencia(Periodicidade.Mensal, 31, antecedencia, D(2026, 10, 1), fim), D(2026, 10, 1));

    public sealed class CriarRecorrencia
    {
        [Fact]
        public void NasceAtivaComAPrimeiraOcorrencia()
        {
            var recorrencia = Criar();

            recorrencia.Status.Should().Be(StatusRecorrencia.Ativa);
            recorrencia.ProximaData.Should().Be(D(2026, 10, 31));
            recorrencia.ClienteId.Should().Be(Contratos.Cliente);
        }

        [Fact]
        public void InicioNoPassado_SoAncoraOCiclo()
        {
            var recorrencia = RecorrenciaTarefa.Criar(
                Guid.NewGuid(), Calendarios.Tenant, Contratos.Cliente, null, Modelo(),
                new RegraRecorrencia(Periodicidade.Trimestral, 10, 0, D(2026, 1, 1)), D(2026, 10, 13));

            recorrencia.ProximaData.Should().Be(D(2027, 1, 10));
            recorrencia.GerarDevidas(D(2026, 10, 13)).Should().BeEmpty();
        }

        [Fact]
        public void ContratoVazio_LancaExcecao()
        {
            var acao = () => RecorrenciaTarefa.Criar(
                Guid.NewGuid(), Calendarios.Tenant, Contratos.Cliente, Guid.Empty, Modelo(),
                new RegraRecorrencia(Periodicidade.Mensal, 31, 3, D(2026, 10, 1)), D(2026, 10, 1));

            acao.Should().Throw<ArgumentException>().WithParameterName("contratoId");
        }
    }

    public sealed class GerarDevidas
    {
        [Fact]
        public void AntesDaAntecedencia_NaoGera()
        {
            var recorrencia = Criar();

            recorrencia.GerarDevidas(D(2026, 10, 27)).Should().BeEmpty();
            recorrencia.ProximaData.Should().Be(D(2026, 10, 31));
        }

        [Fact]
        public void NoDiaDaAntecedencia_GeraEAvanca()
        {
            var recorrencia = Criar();

            recorrencia.GerarDevidas(D(2026, 10, 28)).Should().Equal(D(2026, 10, 31));
            recorrencia.ProximaData.Should().Be(D(2026, 11, 30));
        }

        [Fact]
        public void ChamadaRepetidaNoMesmoDia_NaoDuplica()
        {
            var recorrencia = Criar();
            recorrencia.GerarDevidas(D(2026, 10, 28));

            recorrencia.GerarDevidas(D(2026, 10, 28)).Should().BeEmpty();
        }

        [Fact]
        public void AgendadorParado_GeraTodasAsAtrasadasEmOrdem()
        {
            var recorrencia = Criar();

            recorrencia.GerarDevidas(D(2027, 1, 5)).Should().Equal(D(2026, 10, 31), D(2026, 11, 30), D(2026, 12, 31));
            recorrencia.ProximaData.Should().Be(D(2027, 1, 31));
        }

        [Fact]
        public void SemAntecedencia_GeraNoProprioDia()
        {
            var recorrencia = Criar(antecedencia: 0);

            recorrencia.GerarDevidas(D(2026, 10, 30)).Should().BeEmpty();
            recorrencia.GerarDevidas(D(2026, 10, 31)).Should().Equal(D(2026, 10, 31));
        }

        [Fact]
        public void LimitePorChamada_ContinuaNaProxima()
        {
            var recorrencia = RecorrenciaTarefa.Criar(
                Guid.NewGuid(), Calendarios.Tenant, Contratos.Cliente, null, Modelo(),
                new RegraRecorrencia(Periodicidade.Semanal, 1, 0, D(2020, 1, 1)), D(2020, 1, 1));

            var primeira = recorrencia.GerarDevidas(D(2026, 10, 1));
            var segunda = recorrencia.GerarDevidas(D(2026, 10, 1));

            primeira.Should().HaveCount(RecorrenciaTarefa.MaximoOcorrenciasPorChamada);
            segunda[0].Should().Be(primeira[^1].AddDays(7));
        }

        [Fact]
        public void EsgotaNoFim_EncerraARecorrencia()
        {
            var recorrencia = Criar(fim: D(2026, 11, 30));

            recorrencia.GerarDevidas(D(2026, 12, 31)).Should().Equal(D(2026, 10, 31), D(2026, 11, 30));
            recorrencia.ProximaData.Should().BeNull();
            recorrencia.Status.Should().Be(StatusRecorrencia.Encerrada);
        }

        [Fact]
        public void Pausada_NaoGera()
        {
            var recorrencia = Criar();
            recorrencia.Pausar();

            recorrencia.GerarDevidas(D(2026, 12, 31)).Should().BeEmpty();
            recorrencia.ProximaData.Should().Be(D(2026, 10, 31));
        }
    }

    public sealed class Transicoes
    {
        [Fact]
        public void Retomar_NaoGeraRetroativas()
        {
            var recorrencia = Criar();
            recorrencia.Pausar();

            recorrencia.Retomar(D(2027, 1, 10));

            recorrencia.Status.Should().Be(StatusRecorrencia.Ativa);
            recorrencia.ProximaData.Should().Be(D(2027, 1, 31));
            recorrencia.GerarDevidas(D(2027, 1, 10)).Should().BeEmpty();
        }

        [Fact]
        public void Retomar_DepoisDoFim_Encerra()
        {
            var recorrencia = Criar(fim: D(2026, 11, 30));
            recorrencia.Pausar();

            recorrencia.Retomar(D(2026, 12, 15));

            recorrencia.Status.Should().Be(StatusRecorrencia.Encerrada);
        }

        [Fact]
        public void Pausar_NaoAtiva_LancaExcecao()
        {
            var recorrencia = Criar();
            recorrencia.Pausar();

            ((Action)recorrencia.Pausar).Should().Throw<TransicaoInvalidaException>();
        }

        [Fact]
        public void Retomar_Ativa_LancaExcecao()
        {
            var acao = () => Criar().Retomar(D(2026, 10, 10));

            acao.Should().Throw<TransicaoInvalidaException>();
        }

        [Fact]
        public void Encerrar_DeAtivaOuPausada()
        {
            var ativa = Criar();
            var pausada = Criar();
            pausada.Pausar();

            ativa.Encerrar();
            pausada.Encerrar();

            ativa.Status.Should().Be(StatusRecorrencia.Encerrada);
            pausada.Status.Should().Be(StatusRecorrencia.Encerrada);
        }

        [Fact]
        public void Encerrada_TudoLancaExcecao()
        {
            var recorrencia = Criar();
            recorrencia.Encerrar();

            ((Action)recorrencia.Encerrar).Should().Throw<TransicaoInvalidaException>();
            ((Action)recorrencia.Pausar).Should().Throw<TransicaoInvalidaException>();
            ((Action)(() => recorrencia.Retomar(D(2026, 10, 10)))).Should().Throw<TransicaoInvalidaException>();
            ((Action)(() => recorrencia.AlterarModelo(Modelo()))).Should().Throw<TransicaoInvalidaException>();
            ((Action)(() => recorrencia.AlterarContrato(null))).Should().Throw<TransicaoInvalidaException>();
            recorrencia.GerarDevidas(D(2027, 1, 1)).Should().BeEmpty();
        }

        [Fact]
        public void AlterarRegra_RecalculaAPartirDeHoje()
        {
            var recorrencia = Criar();

            recorrencia.AlterarRegra(new RegraRecorrencia(Periodicidade.Semanal, 1, 0, D(2026, 10, 1)), D(2026, 10, 6));

            recorrencia.ProximaData.Should().Be(D(2026, 10, 12));
        }

        [Fact]
        public void AlterarContrato_TrocaOuRemove()
        {
            var recorrencia = Criar();
            var contrato = Guid.NewGuid();

            recorrencia.AlterarContrato(contrato);
            recorrencia.ContratoId.Should().Be(contrato);
            recorrencia.AlterarContrato(null);
            recorrencia.ContratoId.Should().BeNull();
            ((Action)(() => recorrencia.AlterarContrato(Guid.Empty))).Should().Throw<ArgumentException>();
        }

        [Fact]
        public void AlterarModelo_Substitui()
        {
            var recorrencia = Criar();
            var novo = new ModeloTarefa("Fechamento trimestral", null, null, [], null, false);

            recorrencia.AlterarModelo(novo);

            recorrencia.Modelo.Should().Be(novo);
        }
    }

    public sealed class TarefaGerada
    {
        [Fact]
        public void CriarDeRecorrencia_CopiaOModeloEReferencia()
        {
            var recorrencia = Criar();

            var tarefa = Tarefa.CriarDeRecorrencia(Guid.NewGuid(), recorrencia, D(2026, 10, 31));

            tarefa.TenantId.Should().Be(recorrencia.TenantId);
            tarefa.ClienteId.Should().Be(Contratos.Cliente);
            tarefa.Titulo.Should().Be("Fechamento mensal");
            tarefa.Descricao.Should().Be("Rotina do mês");
            tarefa.Estimativa.Should().Be(Duracao.FromHoras(4));
            tarefa.VisivelCliente.Should().BeTrue();
            tarefa.RecorrenciaId.Should().Be(recorrencia.Id);
            tarefa.DataReferencia.Should().Be(D(2026, 10, 31));
            tarefa.Prazo.Should().Be(D(2026, 10, 31));
            tarefa.Status.Should().Be(StatusTarefa.AFazer);
            tarefa.Checklist.Select(i => i.Texto).Should().Equal("Conciliar extratos", "Emitir guias");
            tarefa.Checklist.Should().OnlyContain(i => !i.Feito);
        }

        [Fact]
        public void CriarDeRecorrencia_ItensComIdsProprios()
        {
            var recorrencia = Criar();

            var a = Tarefa.CriarDeRecorrencia(Guid.NewGuid(), recorrencia, D(2026, 10, 31));
            var b = Tarefa.CriarDeRecorrencia(Guid.NewGuid(), recorrencia, D(2026, 11, 30));

            a.Checklist[0].Id.Should().NotBe(b.Checklist[0].Id);
        }
    }
}
