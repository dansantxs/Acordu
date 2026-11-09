using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.UnitTests.Entities;

public sealed class TarefaTests
{
    private static readonly Guid Solicitacao = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly DateTimeOffset Agora = new(2026, 10, 21, 13, 0, 0, TimeSpan.Zero);

    private static Tarefa Criar(
        Guid? clienteId = null,
        Guid? solicitacaoId = null,
        Guid? projetoId = null,
        bool visivelCliente = false,
        Duracao? estimativa = null) =>
        Tarefa.Criar(
            Guid.NewGuid(), Calendarios.Tenant, "Refazer arte", "Com a data nova", clienteId, solicitacaoId, projetoId,
            responsavelId: null, prazo: new DateOnly(2026, 10, 9), estimativa, visivelCliente);

    private static Tarefa DeSolicitacao(bool visivelCliente = true) =>
        Criar(Contratos.Cliente, Solicitacao, visivelCliente: visivelCliente);

    private static Tarefa Em(StatusTarefa status)
    {
        var tarefa = DeSolicitacao();
        switch (status)
        {
            case StatusTarefa.AFazer:
                break;
            case StatusTarefa.Fazendo:
                tarefa.Iniciar();
                break;
            case StatusTarefa.Concluida:
                tarefa.Iniciar();
                tarefa.Concluir(Agora);
                break;
            case StatusTarefa.Cancelada:
                tarefa.Cancelar();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        tarefa.LimparEventos();
        return tarefa;
    }

    public sealed class CriarTarefa
    {
        [Fact]
        public void DeSolicitacao_NasceAFazerComOsDados()
        {
            var tarefa = Criar(Contratos.Cliente, Solicitacao, visivelCliente: true, estimativa: Duracao.FromHoras(2));

            tarefa.Status.Should().Be(StatusTarefa.AFazer);
            tarefa.Titulo.Should().Be("Refazer arte");
            tarefa.ClienteId.Should().Be(Contratos.Cliente);
            tarefa.SolicitacaoId.Should().Be(Solicitacao);
            tarefa.VisivelCliente.Should().BeTrue();
            tarefa.Estimativa.Should().Be(Duracao.FromHoras(2));
            tarefa.Checklist.Should().BeEmpty();
        }

        [Fact]
        public void AvulsaInterna_SemCliente_Aceita()
        {
            var tarefa = Criar();

            tarefa.ClienteId.Should().BeNull();
            tarefa.VisivelCliente.Should().BeFalse();
        }

        [Fact]
        public void DeSolicitacaoSemCliente_LancaExcecao()
        {
            var acao = () => Criar(solicitacaoId: Solicitacao);

            acao.Should().Throw<ArgumentException>().WithParameterName("clienteId");
        }

        [Fact]
        public void DeProjetoSemCliente_LancaExcecao()
        {
            var acao = () => Criar(projetoId: Guid.NewGuid());

            acao.Should().Throw<ArgumentException>().WithParameterName("clienteId");
        }

        [Fact]
        public void VisivelSemCliente_LancaExcecao()
        {
            var acao = () => Criar(visivelCliente: true);

            acao.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void EstimativaZero_LancaExcecao()
        {
            var acao = () => Criar(estimativa: Duracao.Zero);

            acao.Should().Throw<ArgumentException>().WithParameterName("estimativa");
        }

        [Fact]
        public void SemTitulo_LancaExcecao()
        {
            var acao = () => Tarefa.Criar(
                Guid.NewGuid(), Calendarios.Tenant, " ", null, null, null, null, null, null, null, false);

            acao.Should().Throw<ArgumentException>().WithParameterName("titulo");
        }
    }

    /// <summary>Cada célula da máquina de estados do §4.6.</summary>
    public sealed class MaquinaDeEstados
    {
        private static readonly Dictionary<(StatusTarefa, string), StatusTarefa> Validas = new()
        {
            [(StatusTarefa.AFazer, "Iniciar")] = StatusTarefa.Fazendo,
            [(StatusTarefa.AFazer, "Cancelar")] = StatusTarefa.Cancelada,
            [(StatusTarefa.Fazendo, "Concluir")] = StatusTarefa.Concluida,
            [(StatusTarefa.Fazendo, "Retornar")] = StatusTarefa.AFazer,
            [(StatusTarefa.Fazendo, "Cancelar")] = StatusTarefa.Cancelada,
            [(StatusTarefa.Concluida, "Reabrir")] = StatusTarefa.Fazendo,
        };

        private static readonly string[] Operacoes = ["Iniciar", "Concluir", "Reabrir", "Cancelar", "Retornar"];

        public static TheoryData<StatusTarefa, string> TransicoesValidas()
        {
            var dados = new TheoryData<StatusTarefa, string>();
            foreach (var (origem, operacao) in Validas.Keys)
                dados.Add(origem, operacao);
            return dados;
        }

        public static TheoryData<StatusTarefa, string> TransicoesInvalidas()
        {
            var dados = new TheoryData<StatusTarefa, string>();
            foreach (var (origem, operacao) in CelulasInvalidas())
                dados.Add(origem, operacao);
            return dados;
        }

        [Theory]
        [MemberData(nameof(TransicoesValidas))]
        public void TransicaoDaTabela_LevaAoDestino(StatusTarefa origem, string operacao)
        {
            var tarefa = Em(origem);

            Executar(tarefa, operacao);

            tarefa.Status.Should().Be(Validas[(origem, operacao)]);
        }

        [Theory]
        [MemberData(nameof(TransicoesInvalidas))]
        public void ForaDaTabela_LancaExcecaoSemMudarStatus(StatusTarefa origem, string operacao)
        {
            var tarefa = Em(origem);

            var acao = () => Executar(tarefa, operacao);

            acao.Should().Throw<TransicaoInvalidaException>();
            tarefa.Status.Should().Be(origem);
        }

        [Fact]
        public void Matriz_CobreTodasAsCelulas()
        {
            (Validas.Count + CelulasInvalidas().Count())
                .Should().Be(Enum.GetValues<StatusTarefa>().Length * Operacoes.Length);
        }

        [Fact]
        public void Concluir_EmiteTarefaConcluida()
        {
            var tarefa = Em(StatusTarefa.Fazendo);

            tarefa.Concluir(Agora);

            tarefa.Eventos.Should().HaveCount(2);
            var status = (TarefaStatusAlterada)tarefa.Eventos[0];
            (status.De, status.Para).Should().Be((StatusTarefa.Fazendo, StatusTarefa.Concluida));
            tarefa.Eventos[1].Should().Be(new TarefaConcluida(tarefa.Id, Calendarios.Tenant, Contratos.Cliente, Solicitacao, null));
        }

        [Fact]
        public void Concluir_GuardaQuandoEReabrirZera()
        {
            var tarefa = Em(StatusTarefa.Fazendo);

            tarefa.Concluir(Agora);
            var concluida = tarefa.ConcluidaEm;
            tarefa.Reabrir();

            concluida.Should().Be(Agora);
            tarefa.ConcluidaEm.Should().BeNull();
        }

        [Fact]
        public void Retornar_VoltaParaAFazerEmitindoStatus()
        {
            var tarefa = Em(StatusTarefa.Fazendo);

            tarefa.Retornar();

            tarefa.Eventos.Should().HaveCount(1);
            var status = (TarefaStatusAlterada)tarefa.Eventos[0];
            (status.De, status.Para).Should().Be((StatusTarefa.Fazendo, StatusTarefa.AFazer));
        }

        [Fact]
        public void Transicoes_TemOcorrenciasDistintas()
        {
            var tarefa = Em(StatusTarefa.Fazendo);

            tarefa.Concluir(Agora);
            tarefa.Reabrir();
            tarefa.Concluir(Agora);

            var ocorrencias = tarefa.Eventos.OfType<TarefaStatusAlterada>().Select(e => e.Ocorrencia).ToList();
            ocorrencias.Should().HaveCount(3);
            ocorrencias.Distinct().Count().Should().Be(3);
        }

        [Fact]
        public void Criar_EmiteTarefaCriadaComResponsavel()
        {
            var responsavel = Guid.NewGuid();

            var tarefa = Tarefa.Criar(
                Guid.NewGuid(), Calendarios.Tenant, "Arte", null, null, null, null, responsavel, null, null, false);

            tarefa.Eventos.Should().HaveCount(1);
            tarefa.Eventos[0].Should().Be(new TarefaCriada(tarefa.Id, Calendarios.Tenant, responsavel));
        }

        [Fact]
        public void DeSolicitacao_EventosLevamOSolicitacaoId() // F11-18, F11-19
        {
            var cliente = Guid.NewGuid();
            var solicitacao = Guid.NewGuid();

            var tarefa = Tarefa.Criar(
                Guid.NewGuid(), Calendarios.Tenant, "Arte", null, cliente, solicitacao, null, null, null, null, false);
            tarefa.Iniciar();

            tarefa.Eventos.OfType<TarefaCriada>().Single().SolicitacaoId.Should().Be(solicitacao);
            tarefa.Eventos.OfType<TarefaStatusAlterada>().Single().SolicitacaoId.Should().Be(solicitacao);
        }

        [Fact]
        public void Atribuir_SoEmiteQuandoMuda()
        {
            var tarefa = Em(StatusTarefa.AFazer);
            var nova = Guid.NewGuid();

            tarefa.Atribuir(nova);
            tarefa.Atribuir(nova);

            tarefa.Eventos.Should().HaveCount(1);
            tarefa.Eventos[0].Should().Be(new TarefaAtribuida(tarefa.Id, Calendarios.Tenant, nova, null));
        }

        private static IEnumerable<(StatusTarefa Origem, string Operacao)> CelulasInvalidas() =>
            from origem in Enum.GetValues<StatusTarefa>()
            from operacao in Operacoes
            where !Validas.ContainsKey((origem, operacao))
            select (origem, operacao);

        private static void Executar(Tarefa tarefa, string operacao)
        {
            switch (operacao)
            {
                case "Iniciar": tarefa.Iniciar(); break;
                case "Concluir": tarefa.Concluir(Agora); break;
                case "Retornar": tarefa.Retornar(); break;
                case "Reabrir": tarefa.Reabrir(); break;
                case "Cancelar": tarefa.Cancelar(); break;
                default: throw new ArgumentOutOfRangeException(nameof(operacao));
            }
        }
    }

    public sealed class Edicao
    {
        [Fact]
        public void Editar_AlteraTituloEDescricao()
        {
            var tarefa = DeSolicitacao();

            tarefa.Editar("Novo", "  ");

            tarefa.Titulo.Should().Be("Novo");
            tarefa.Descricao.Should().BeNull();
        }

        [Fact]
        public void AtribuirPrazoEstimativa_Alteram()
        {
            var responsavel = Guid.NewGuid();
            var tarefa = DeSolicitacao();

            tarefa.Atribuir(responsavel);
            tarefa.DefinirPrazo(null);
            tarefa.DefinirEstimativa(Duracao.FromMinutos(45));

            tarefa.ResponsavelId.Should().Be(responsavel);
            tarefa.Prazo.Should().BeNull();
            tarefa.Estimativa.Should().Be(Duracao.FromMinutos(45));
        }

        [Fact]
        public void Planejar_MudaSoODiaPlanejadoENaoOPrazo()
        {
            var tarefa = DeSolicitacao();

            tarefa.Planejar(new DateOnly(2026, 10, 7));

            tarefa.PlanejadaPara.Should().Be(new DateOnly(2026, 10, 7));
            tarefa.Prazo.Should().Be(new DateOnly(2026, 10, 9));

            tarefa.Planejar(null);

            tarefa.PlanejadaPara.Should().BeNull();
        }

        [Fact]
        public void Criar_ComDiaPlanejado_Guarda()
        {
            var tarefa = Tarefa.Criar(
                Guid.NewGuid(), Calendarios.Tenant, "Arte", null, null, null, null, null, null, null, false,
                new DateOnly(2026, 10, 12));

            tarefa.PlanejadaPara.Should().Be(new DateOnly(2026, 10, 12));
        }

        [Fact]
        public void TornarVisivel_SemCliente_LancaExcecao()
        {
            var tarefa = Criar();

            var acao = () => tarefa.TornarVisivelAoCliente(true);

            acao.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void TornarInvisivel_ComCliente_Aceita()
        {
            var tarefa = DeSolicitacao();

            tarefa.TornarVisivelAoCliente(false);

            tarefa.VisivelCliente.Should().BeFalse();
        }

        [Fact]
        public void Concluida_AindaPodeSerEditada()
        {
            var tarefa = Em(StatusTarefa.Concluida);

            tarefa.Editar("Ajuste do título", null);

            tarefa.Titulo.Should().Be("Ajuste do título");
        }

        [Fact]
        public void Cancelada_EdicoesLancamExcecao()
        {
            var tarefa = Em(StatusTarefa.Cancelada);

            ((Action)(() => tarefa.Editar("Outro", null))).Should().Throw<TransicaoInvalidaException>();
            ((Action)(() => tarefa.Atribuir(Guid.NewGuid()))).Should().Throw<TransicaoInvalidaException>();
            ((Action)(() => tarefa.DefinirPrazo(null))).Should().Throw<TransicaoInvalidaException>();
            ((Action)(() => tarefa.DefinirEstimativa(null))).Should().Throw<TransicaoInvalidaException>();
            ((Action)(() => tarefa.Planejar(null))).Should().Throw<TransicaoInvalidaException>();
            ((Action)(() => tarefa.TornarVisivelAoCliente(false))).Should().Throw<TransicaoInvalidaException>();
            ((Action)(() => tarefa.AdicionarItem("Item"))).Should().Throw<TransicaoInvalidaException>();
        }
    }

    /// <summary>F11-23 a F11-26: vincular, trocar ou tirar o projeto.</summary>
    public sealed class MoverParaProjeto
    {
        private static Projeto NovoProjeto(Guid? clienteId = null, Guid? tenantId = null) =>
            Projeto.Criar(
                Guid.NewGuid(), tenantId ?? Calendarios.Tenant, clienteId ?? Contratos.Cliente, "Site novo", null, null, null);

        [Theory]
        [InlineData(StatusTarefa.AFazer)]
        [InlineData(StatusTarefa.Fazendo)]
        [InlineData(StatusTarefa.Concluida)]
        public void ParaProjetoDoMesmoCliente_MudaEEmite(StatusTarefa status)
        {
            var tarefa = Em(status);
            var projeto = NovoProjeto();

            tarefa.MoverParaProjeto(projeto);

            tarefa.ProjetoId.Should().Be(projeto.Id);
            tarefa.Eventos.Should().Equal(new TarefaProjetoAlterado(tarefa.Id, Calendarios.Tenant, null, projeto.Id));
        }

        [Fact]
        public void TrocarETirar_GuardaDeOndeVeio()
        {
            var tarefa = Em(StatusTarefa.AFazer);
            var primeiro = NovoProjeto();
            var segundo = NovoProjeto();
            tarefa.MoverParaProjeto(primeiro);

            tarefa.MoverParaProjeto(segundo);
            tarefa.MoverParaProjeto(null);

            tarefa.ProjetoId.Should().BeNull();
            tarefa.Eventos.OfType<TarefaProjetoAlterado>().Select(e => (e.De, e.Para)).Should().Equal(
                (null, primeiro.Id), (primeiro.Id, segundo.Id), (segundo.Id, null));
        }

        [Fact]
        public void MesmoProjeto_NaoEmite()
        {
            var tarefa = Em(StatusTarefa.AFazer);
            var projeto = NovoProjeto();
            tarefa.MoverParaProjeto(projeto);
            tarefa.LimparEventos();

            tarefa.MoverParaProjeto(projeto);

            tarefa.Eventos.Should().BeEmpty();
        }

        [Fact]
        public void SemProjeto_TirarDeNovo_NaoEmite()
        {
            var tarefa = Em(StatusTarefa.AFazer);

            tarefa.MoverParaProjeto(null);

            tarefa.Eventos.Should().BeEmpty();
        }

        [Fact]
        public void ProjetoDeOutroCliente_Recusa() // F11-24
        {
            var tarefa = Em(StatusTarefa.AFazer);

            var acao = () => tarefa.MoverParaProjeto(NovoProjeto(Guid.NewGuid()));

            acao.Should().Throw<ProjetoDeOutroClienteException>();
            tarefa.ProjetoId.Should().BeNull();
        }

        [Fact]
        public void TarefaSemCliente_NaoEntraEmProjeto()
        {
            var tarefa = Criar();

            var acao = () => tarefa.MoverParaProjeto(NovoProjeto());

            acao.Should().Throw<ProjetoDeOutroClienteException>();
        }

        [Fact]
        public void ProjetoDeOutroTenant_Recusa()
        {
            var tarefa = Em(StatusTarefa.AFazer);

            var acao = () => tarefa.MoverParaProjeto(NovoProjeto(tenantId: Guid.NewGuid()));

            acao.Should().Throw<ProjetoDeOutroClienteException>();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ProjetoEncerrado_Recusa(bool concluido) // F11-25
        {
            var tarefa = Em(StatusTarefa.AFazer);
            var projeto = NovoProjeto();
            if (concluido)
                projeto.Concluir();
            else
                projeto.Cancelar();

            var acao = () => tarefa.MoverParaProjeto(projeto);

            acao.Should().Throw<ProjetoEncerradoException>();
        }

        [Fact]
        public void SairDeProjetoEncerrado_Pode()
        {
            var projeto = NovoProjeto();
            var tarefa = Criar(Contratos.Cliente, projetoId: projeto.Id);
            projeto.Concluir();

            tarefa.MoverParaProjeto(null);

            tarefa.ProjetoId.Should().BeNull();
        }

        [Fact]
        public void TarefaCancelada_Recusa()
        {
            var tarefa = Em(StatusTarefa.Cancelada);

            var acao = () => tarefa.MoverParaProjeto(NovoProjeto());

            acao.Should().Throw<TransicaoInvalidaException>();
        }
    }

    public sealed class Checklist
    {
        [Fact]
        public void AdicionarItem_EntraNoFimNaoFeito()
        {
            var tarefa = DeSolicitacao();

            tarefa.AdicionarItem("Conciliar extratos");
            var item = tarefa.AdicionarItem(" Emitir guias ");

            tarefa.Checklist.Should().HaveCount(2);
            tarefa.Checklist[1].Should().BeSameAs(item);
            item.Texto.Should().Be("Emitir guias");
            item.Feito.Should().BeFalse();
            item.Id.Should().NotBe(Guid.Empty);
        }

        [Fact]
        public void AdicionarItem_AlemDoLimite_LancaExcecao()
        {
            var tarefa = DeSolicitacao();
            for (var i = 0; i < Tarefa.MaximoItensChecklist; i++)
                tarefa.AdicionarItem($"Item {i}");

            var acao = () => tarefa.AdicionarItem("Mais um");

            acao.Should().Throw<ArgumentException>();
            tarefa.Checklist.Should().HaveCount(Tarefa.MaximoItensChecklist);
        }

        [Fact]
        public void AdicionarItem_TextoVazio_LancaExcecao()
        {
            var acao = () => DeSolicitacao().AdicionarItem("  ");

            acao.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void MarcarEditarRemover_AtuamNoItem()
        {
            var tarefa = DeSolicitacao();
            var primeiro = tarefa.AdicionarItem("Um");
            var segundo = tarefa.AdicionarItem("Dois");

            tarefa.MarcarItem(primeiro.Id, true);
            tarefa.EditarItem(segundo.Id, "Dois revisado");
            tarefa.RemoverItem(primeiro.Id);

            tarefa.Checklist.Should().ContainSingle();
            tarefa.Checklist[0].Texto.Should().Be("Dois revisado");
            primeiro.Feito.Should().BeTrue();
        }

        [Fact]
        public void Ordem_SobreviveARemocaoEContinuaCrescendo()
        {
            var tarefa = DeSolicitacao();
            tarefa.AdicionarItem("Um");
            var dois = tarefa.AdicionarItem("Dois");
            tarefa.AdicionarItem("Três");

            tarefa.RemoverItem(dois.Id);
            var quatro = tarefa.AdicionarItem("Quatro");

            tarefa.Checklist.Select(i => i.Texto).Should().Equal("Um", "Três", "Quatro");
            quatro.Ordem.Should().Be(4);
        }

        [Fact]
        public void ItemInexistente_LancaExcecao()
        {
            var tarefa = DeSolicitacao();

            var acao = () => tarefa.MarcarItem(Guid.NewGuid(), true);

            acao.Should().Throw<ArgumentException>().WithParameterName("itemId");
        }

        [Fact]
        public void ConcluidaPodeMarcarItem()
        {
            var tarefa = DeSolicitacao();
            var item = tarefa.AdicionarItem("Revisar");
            tarefa.Iniciar();
            tarefa.Concluir(Agora);

            tarefa.MarcarItem(item.Id, true);

            item.Feito.Should().BeTrue();
        }
    }
}
