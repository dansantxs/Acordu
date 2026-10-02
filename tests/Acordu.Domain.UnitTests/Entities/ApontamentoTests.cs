using Acordu.Domain.Entities;
using Acordu.Domain.Events;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.UnitTests.Entities;

public sealed class ApontamentoTests
{
    private static readonly Guid Tarefa = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid Usuario = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid Contrato = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateOnly Dia30Set = new(2026, 9, 30);
    private static readonly DateOnly Dia1Out = new(2026, 10, 1);
    private static readonly MesReferencia[] SetembroFechado = [new(2026, 9)];
    private static readonly MesReferencia[] NenhumFechado = [];

    private static Apontamento Registrar(
        DateOnly? data = null,
        int minutos = 90,
        bool faturavel = true,
        bool? visivel = null,
        string? externa = null,
        Guid? cliente = null,
        bool semCliente = false,
        bool semContrato = false,
        IReadOnlyCollection<MesReferencia>? fechados = null) =>
        Apontamento.Registrar(
            Guid.NewGuid(), Calendarios.Tenant, Tarefa, semCliente ? null : cliente ?? Contratos.Cliente, Usuario,
            semContrato ? null : Contrato, data ?? Dia1Out, Duracao.FromMinutos(minutos), "Ajuste de layout", externa,
            faturavel, visivel, fechados ?? NenhumFechado);

    public sealed class RegistrarApontamento
    {
        [Fact]
        public void Valido_GuardaDadosEEmiteEvento()
        {
            var apontamento = Registrar(externa: "Arte refeita");

            apontamento.TarefaId.Should().Be(Tarefa);
            apontamento.ContratoId.Should().Be(Contrato);
            apontamento.Duracao.Should().Be(Duracao.FromMinutos(90));
            apontamento.DescricaoExterna.Should().Be("Arte refeita");
            apontamento.Eventos.Should().ContainSingle();
            apontamento.Eventos[0].Should().Be(new ApontamentoRegistrado(
                apontamento.Id, Calendarios.Tenant, Contratos.Cliente, Contrato, Dia1Out, Duracao.FromMinutos(90), true));
        }

        [Fact]
        public void Padrao_VisivelSeFaturavel()
        {
            Registrar(faturavel: true).VisivelCliente.Should().BeTrue();
            Registrar(faturavel: false).VisivelCliente.Should().BeFalse();
        }

        [Fact]
        public void Padrao_SemCliente_NaoFicaVisivel()
        {
            Registrar(faturavel: true, semCliente: true, semContrato: true).VisivelCliente.Should().BeFalse();
        }

        [Fact]
        public void VisibilidadeAjustavel_FaturavelOculto()
        {
            Registrar(faturavel: true, visivel: false).VisivelCliente.Should().BeFalse();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1441)]
        public void DuracaoForaDoIntervalo_LancaExcecao(int minutos)
        {
            var acao = () => Registrar(minutos: minutos);

            acao.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("duracao");
        }

        [Fact]
        public void DuracaoDe24h_Aceita()
        {
            Registrar(minutos: 1440).Duracao.Should().Be(Duracao.FromHoras(24));
        }

        [Fact]
        public void DescricaoExternaSemVisibilidade_LancaExcecao()
        {
            var acao = () => Registrar(visivel: false, externa: "Texto para o cliente");

            acao.Should().Throw<ArgumentException>().WithParameterName("descricaoExterna");
        }

        [Fact]
        public void VisivelSemCliente_LancaExcecao()
        {
            var acao = () => Registrar(visivel: true, semCliente: true, semContrato: true);

            acao.Should().Throw<ArgumentException>().WithParameterName("visivelCliente");
        }

        [Fact]
        public void SemDescricao_LancaExcecao()
        {
            var acao = () => Apontamento.Registrar(
                Guid.NewGuid(), Calendarios.Tenant, Tarefa, null, Usuario, null, Dia1Out, Duracao.FromMinutos(30), " ",
                null, false, null, NenhumFechado);

            acao.Should().Throw<ArgumentException>().WithParameterName("descricao");
        }

        [Fact]
        public void TarefaVazia_LancaExcecao()
        {
            var acao = () => Apontamento.Registrar(
                Guid.NewGuid(), Calendarios.Tenant, Guid.Empty, null, Usuario, null, Dia1Out, Duracao.FromMinutos(30), "x",
                null, false, null, NenhumFechado);

            acao.Should().Throw<ArgumentException>().WithParameterName("tarefaId");
        }

        [Fact]
        public void EmPeriodoFechado_LancaExcecao()
        {
            var acao = () => Registrar(data: Dia30Set, fechados: SetembroFechado);

            acao.Should().Throw<PeriodoFechadoException>().WithMessage("*2026-09*");
        }

        [Fact]
        public void NoMesSeguinteAoFechado_Aceita()
        {
            var acao = () => Registrar(data: Dia1Out, fechados: SetembroFechado);

            acao.Should().NotThrow();
        }

        [Fact]
        public void SemContrato_IgnoraPeriodosFechados()
        {
            var acao = () => Registrar(data: Dia30Set, semContrato: true, fechados: SetembroFechado);

            acao.Should().NotThrow();
        }
    }

    public sealed class Editar
    {
        [Fact]
        public void Valido_AlteraEEmiteEventoComAsDuasDatas()
        {
            var apontamento = Registrar(data: Dia30Set);
            apontamento.LimparEventos();

            apontamento.Editar(Dia1Out, Duracao.FromMinutos(60), "Revisão", null, false, false, NenhumFechado);

            apontamento.Data.Should().Be(Dia1Out);
            apontamento.Duracao.Should().Be(Duracao.FromMinutos(60));
            apontamento.Faturavel.Should().BeFalse();
            apontamento.Eventos.Should().ContainSingle();
            apontamento.Eventos[0].Should().Be(new ApontamentoAlterado(
                apontamento.Id, Calendarios.Tenant, Contratos.Cliente, Contrato, Dia30Set, Dia1Out));
        }

        [Fact]
        public void DataAtualEmPeriodoFechado_LancaExcecaoSemAlterar()
        {
            var apontamento = Registrar(data: Dia30Set);

            var acao = () => apontamento.Editar(Dia1Out, Duracao.FromMinutos(60), "x", null, true, true, SetembroFechado);

            acao.Should().Throw<PeriodoFechadoException>();
            apontamento.Data.Should().Be(Dia30Set);
        }

        [Fact]
        public void NovaDataEmPeriodoFechado_LancaExcecaoSemAlterar()
        {
            var apontamento = Registrar(data: Dia1Out);

            var acao = () => apontamento.Editar(Dia30Set, Duracao.FromMinutos(60), "x", null, true, true, SetembroFechado);

            acao.Should().Throw<PeriodoFechadoException>();
            apontamento.Data.Should().Be(Dia1Out);
            apontamento.Duracao.Should().Be(Duracao.FromMinutos(90));
        }

        [Fact]
        public void TornarInvisivelMantendoDescricaoExterna_LancaExcecao()
        {
            var apontamento = Registrar(externa: "Arte refeita");

            var acao = () => apontamento.Editar(Dia1Out, Duracao.FromMinutos(90), "x", "Arte refeita", true, false, NenhumFechado);

            acao.Should().Throw<ArgumentException>().WithParameterName("descricaoExterna");
            apontamento.VisivelCliente.Should().BeTrue();
        }
    }

    public sealed class Excluir
    {
        [Fact]
        public void PeriodoAberto_EmiteEvento()
        {
            var apontamento = Registrar();
            apontamento.LimparEventos();

            apontamento.Excluir(NenhumFechado);

            apontamento.Eventos.Should().ContainSingle();
            apontamento.Eventos[0].Should().Be(new ApontamentoExcluido(
                apontamento.Id, Calendarios.Tenant, Contratos.Cliente, Contrato, Dia1Out));
        }

        [Fact]
        public void PeriodoFechado_LancaExcecao()
        {
            var apontamento = Registrar(data: Dia30Set);
            apontamento.LimparEventos();

            var acao = () => apontamento.Excluir(SetembroFechado);

            acao.Should().Throw<PeriodoFechadoException>();
            apontamento.Eventos.Should().BeEmpty();
        }
    }
}
