using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;
using static Acordu.Domain.UnitTests.Contratos;

namespace Acordu.Domain.UnitTests.Entities;

public sealed class ContratoTests
{
    private static readonly DateOnly FimOutubro = new(2026, 10, 31);

    public sealed class Criar
    {
        [Fact]
        public void BancoDeHoras_NasceAtivoComOsDadosInformados()
        {
            var contrato = Contratos.Criar(TipoContrato.BancoHoras, Duracao.FromHoras(20));

            contrato.Status.Should().Be(StatusContrato.Ativo);
            contrato.Tipo.Should().Be(TipoContrato.BancoHoras);
            contrato.Franquia.Should().Be(Duracao.FromHoras(20));
            contrato.ClienteId.Should().Be(Contratos.Cliente);
            contrato.Vigencia.Should().Be(new Periodo(InicioOutubro));
            contrato.Sla.Should().Be(PoliticaPadrao());
        }

        [Fact]
        public void MensalFixoSemFranquia_Aceita()
        {
            Contratos.Criar(TipoContrato.MensalFixo).Franquia.Should().BeNull();
        }

        [Fact]
        public void MensalFixoComFranquia_Aceita()
        {
            Contratos.Criar(TipoContrato.MensalFixo, Duracao.FromHoras(10)).Franquia.Should().Be(Duracao.FromHoras(10));
        }

        [Fact]
        public void MensalFixoComFranquiaZero_LancaExcecao()
        {
            var acao = () => Contratos.Criar(TipoContrato.MensalFixo, Duracao.Zero);

            acao.Should().Throw<ContratoIncoerenteException>();
        }

        [Fact]
        public void BancoDeHorasSemFranquia_LancaExcecao()
        {
            var acao = () => Contrato.Criar(
                Guid.NewGuid(), Calendarios.Tenant, Contratos.Cliente, TipoContrato.BancoHoras,
                new Periodo(InicioOutubro), null, PoliticaPadrao());

            acao.Should().Throw<ContratoIncoerenteException>();
        }

        [Fact]
        public void BancoDeHorasComFranquiaZero_LancaExcecao()
        {
            var acao = () => Contratos.Criar(TipoContrato.BancoHoras, Duracao.Zero);

            acao.Should().Throw<ContratoIncoerenteException>();
        }

        [Fact]
        public void AvulsoSemFranquia_Aceita()
        {
            Contratos.Criar(TipoContrato.Avulso).Franquia.Should().BeNull();
        }

        [Fact]
        public void AvulsoComFranquia_LancaExcecao()
        {
            var acao = () => Contratos.Criar(TipoContrato.Avulso, Duracao.FromHoras(5));

            acao.Should().Throw<ContratoIncoerenteException>();
        }

        [Fact]
        public void TipoInvalido_LancaExcecao()
        {
            var acao = () => Contratos.Criar((TipoContrato)99);

            acao.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void ClienteVazio_LancaExcecao()
        {
            var acao = () => Contrato.Criar(
                Guid.NewGuid(), Calendarios.Tenant, Guid.Empty, TipoContrato.Avulso,
                new Periodo(InicioOutubro), null, PoliticaPadrao());

            acao.Should().Throw<ArgumentException>();
        }
    }

    public sealed class AlterarFranquia
    {
        [Fact]
        public void CoerenteComOTipo_Altera()
        {
            var contrato = Contratos.Criar(TipoContrato.BancoHoras, Duracao.FromHoras(20));

            contrato.AlterarFranquia(Duracao.FromHoras(30));

            contrato.Franquia.Should().Be(Duracao.FromHoras(30));
        }

        [Fact]
        public void RemoverDeBancoDeHoras_LancaExcecao()
        {
            var contrato = Contratos.Criar(TipoContrato.BancoHoras);

            var acao = () => contrato.AlterarFranquia(null);

            acao.Should().Throw<ContratoIncoerenteException>();
        }

        [Fact]
        public void ContratoEncerrado_LancaExcecao()
        {
            var contrato = Contratos.Criar(TipoContrato.MensalFixo);
            contrato.Encerrar(FimOutubro);

            var acao = () => contrato.AlterarFranquia(Duracao.FromHoras(10));

            acao.Should().Throw<TransicaoInvalidaException>();
        }

        [Fact]
        public void ContratoSuspenso_Altera()
        {
            var contrato = Contratos.Criar(TipoContrato.MensalFixo);
            contrato.Suspender();

            contrato.AlterarFranquia(Duracao.FromHoras(10));

            contrato.Franquia.Should().Be(Duracao.FromHoras(10));
        }
    }

    public sealed class DefinirSla
    {
        [Fact]
        public void NovaPolitica_Substitui()
        {
            var contrato = Contratos.Criar();
            var nova = new PoliticaSla(
                Enum.GetValues<Prioridade>().Select(p => new PrazoSla(p, Duracao.FromHoras(2), Duracao.FromHoras(10))));

            contrato.DefinirSla(nova);

            contrato.Sla.Should().Be(nova);
        }

        [Fact]
        public void ContratoEncerrado_LancaExcecao()
        {
            var contrato = Contratos.Criar();
            contrato.Encerrar(FimOutubro);

            var acao = () => contrato.DefinirSla(PoliticaPadrao());

            acao.Should().Throw<TransicaoInvalidaException>();
        }
    }

    public sealed class AlterarFimVigencia
    {
        [Fact]
        public void DefinirFim_MantemInicio()
        {
            var contrato = Contratos.Criar();

            contrato.AlterarFimVigencia(FimOutubro);

            contrato.Vigencia.Should().Be(new Periodo(InicioOutubro, FimOutubro));
        }

        [Fact]
        public void RemoverFim_DeixaEmAberto()
        {
            var contrato = Contratos.Criar(fim: FimOutubro);

            contrato.AlterarFimVigencia(null);

            contrato.Vigencia.EmAberto.Should().BeTrue();
        }

        [Fact]
        public void FimAntesDoInicio_LancaExcecao()
        {
            var contrato = Contratos.Criar();

            var acao = () => contrato.AlterarFimVigencia(InicioOutubro.AddDays(-1));

            acao.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void ContratoEncerrado_LancaExcecao()
        {
            var contrato = Contratos.Criar();
            contrato.Encerrar(FimOutubro);

            var acao = () => contrato.AlterarFimVigencia(null);

            acao.Should().Throw<TransicaoInvalidaException>();
        }
    }

    /// <summary>Cada célula da máquina de estados: ATIVO ↔ SUSPENSO; ATIVO/SUSPENSO → ENCERRADO.</summary>
    public sealed class Transicoes
    {
        [Fact]
        public void Suspender_Ativo_FicaSuspenso()
        {
            var contrato = Contratos.Criar();

            contrato.Suspender();

            contrato.Status.Should().Be(StatusContrato.Suspenso);
        }

        [Fact]
        public void Suspender_Suspenso_LancaExcecao()
        {
            var contrato = Contratos.Criar();
            contrato.Suspender();

            var acao = contrato.Suspender;

            acao.Should().Throw<TransicaoInvalidaException>();
        }

        [Fact]
        public void Suspender_Encerrado_LancaExcecao()
        {
            var contrato = Contratos.Criar();
            contrato.Encerrar(FimOutubro);

            var acao = contrato.Suspender;

            acao.Should().Throw<TransicaoInvalidaException>();
        }

        [Fact]
        public void Reativar_Suspenso_FicaAtivo()
        {
            var contrato = Contratos.Criar();
            contrato.Suspender();

            contrato.Reativar();

            contrato.Status.Should().Be(StatusContrato.Ativo);
        }

        [Fact]
        public void Reativar_Ativo_LancaExcecao()
        {
            var contrato = Contratos.Criar();

            var acao = contrato.Reativar;

            acao.Should().Throw<TransicaoInvalidaException>();
        }

        [Fact]
        public void Reativar_Encerrado_LancaExcecao()
        {
            var contrato = Contratos.Criar();
            contrato.Encerrar(FimOutubro);

            var acao = contrato.Reativar;

            acao.Should().Throw<TransicaoInvalidaException>();
        }

        [Fact]
        public void Encerrar_Ativo_FicaEncerradoComFimNaData()
        {
            var contrato = Contratos.Criar();

            contrato.Encerrar(FimOutubro);

            contrato.Status.Should().Be(StatusContrato.Encerrado);
            contrato.Vigencia.Fim.Should().Be(FimOutubro);
        }

        [Fact]
        public void Encerrar_Suspenso_FicaEncerrado()
        {
            var contrato = Contratos.Criar();
            contrato.Suspender();

            contrato.Encerrar(FimOutubro);

            contrato.Status.Should().Be(StatusContrato.Encerrado);
        }

        [Fact]
        public void Encerrar_Encerrado_LancaExcecao()
        {
            var contrato = Contratos.Criar();
            contrato.Encerrar(FimOutubro);

            var acao = () => contrato.Encerrar(FimOutubro);

            acao.Should().Throw<TransicaoInvalidaException>();
        }

        [Fact]
        public void Encerrar_DataDepoisDoFimJaDefinido_MantemOFimAtual()
        {
            var contrato = Contratos.Criar(fim: new DateOnly(2026, 10, 15));

            contrato.Encerrar(FimOutubro);

            contrato.Vigencia.Fim.Should().Be(new DateOnly(2026, 10, 15));
        }

        [Fact]
        public void Encerrar_DataAntesDoInicio_LancaExcecaoESemMudarStatus()
        {
            var contrato = Contratos.Criar();

            var acao = () => contrato.Encerrar(InicioOutubro.AddDays(-1));

            acao.Should().Throw<ArgumentException>();
            contrato.Status.Should().Be(StatusContrato.Ativo);
        }
    }

    public sealed class EstaVigenteEm
    {
        [Theory]
        [InlineData(2026, 9, 30, false)]
        [InlineData(2026, 10, 1, true)]
        [InlineData(2026, 10, 31, true)]
        [InlineData(2026, 11, 1, false)]
        public void Ativo_DependeDaVigencia(int ano, int mes, int dia, bool esperado)
        {
            var contrato = Contratos.Criar(fim: FimOutubro);

            contrato.EstaVigenteEm(new DateOnly(ano, mes, dia)).Should().Be(esperado);
        }

        [Fact]
        public void Suspenso_NaoEstaVigente()
        {
            var contrato = Contratos.Criar();
            contrato.Suspender();

            contrato.EstaVigenteEm(new DateOnly(2026, 10, 10)).Should().BeFalse();
        }

        [Fact]
        public void Encerrado_NaoEstaVigenteNemDentroDaVigencia()
        {
            var contrato = Contratos.Criar();
            contrato.Encerrar(FimOutubro);

            contrato.EstaVigenteEm(new DateOnly(2026, 10, 10)).Should().BeFalse();
        }
    }
}
