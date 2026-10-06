using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;
using static Acordu.Domain.UnitTests.Calendarios;
using static Acordu.Domain.UnitTests.Solicitacoes;

namespace Acordu.Domain.UnitTests.Entities;

public sealed class SolicitacaoTests
{
    public sealed class AbrirSolicitacao
    {
        [Fact]
        public void ComContrato_NasceAbertaComSlaDaPrioridade()
        {
            var solicitacao = Abrir(prioridade: Prioridade.Media);

            solicitacao.Status.Should().Be(StatusSolicitacao.Aberta);
            solicitacao.StatusExterno.Should().Be(StatusExterno.Recebida);
            solicitacao.Numero.Should().Be(1042);
            solicitacao.Titulo.Should().Be("Ajustar banner da campanha");
            solicitacao.ContratoId.Should().Be(ContratoId);
            solicitacao.AbertaEm.Should().Be(Abertura);
            solicitacao.Sla.Should().NotBeNull();
            solicitacao.Sla!.TempoResolucao.Should().Be(Duracao.FromHoras(32));
            solicitacao.Sla.PrazoPrimeiraResposta.Should().Be(Brasilia(6, 14));
        }

        [Fact]
        public void SemContrato_NaoTemSla()
        {
            var solicitacao = Abrir(comContrato: false);

            solicitacao.Sla.Should().BeNull();
            solicitacao.SituacaoSla(Brasilia(9, 10), Padrao()).Should().Be(SituacaoSla.SemSla);
        }

        [Fact]
        public void RegistraEventoDeAbertura()
        {
            var solicitacao = Abrir();

            solicitacao.Eventos.Should().ContainSingle();
            solicitacao.Eventos[0].Should().Be(new SolicitacaoAberta(
                solicitacao.Id, Calendarios.Tenant, Contratos.Cliente, 1042, OrigemSolicitacao.Interna, Abertura));
        }

        [Fact]
        public void ContratoSemPolitica_LancaExcecao()
        {
            var acao = () => Solicitacao.Abrir(
                Guid.NewGuid(), Calendarios.Tenant, Contratos.Cliente, 1, "Título", null, Prioridade.Baixa, OrigemSolicitacao.Portal,
                null, null, null, ContratoId, null, Abertura, Padrao());

            acao.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void PoliticaSemContrato_LancaExcecao()
        {
            var acao = () => Solicitacao.Abrir(
                Guid.NewGuid(), Calendarios.Tenant, Contratos.Cliente, 1, "Título", null, Prioridade.Baixa, OrigemSolicitacao.Portal,
                null, null, null, null, Contratos.PoliticaPadrao(), Abertura, Padrao());

            acao.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void NumeroNaoPositivo_LancaExcecao(int numero)
        {
            var acao = () => Solicitacao.Abrir(
                Guid.NewGuid(), Calendarios.Tenant, Contratos.Cliente, numero, "Título", null, Prioridade.Baixa, OrigemSolicitacao.Portal,
                null, null, null, null, null, Abertura, Padrao());

            acao.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void SemTitulo_LancaExcecao(string tituloInformado)
        {
            var acao = () => Solicitacao.Abrir(
                Guid.NewGuid(), Calendarios.Tenant, Contratos.Cliente, 1, tituloInformado, null, Prioridade.Baixa, OrigemSolicitacao.Portal,
                null, null, null, null, null, Abertura, Padrao());

            acao.Should().Throw<ArgumentException>().WithParameterName("titulo");
        }

        [Fact]
        public void DescricaoSoComEspacos_ViraNula()
        {
            var solicitacao = Solicitacao.Abrir(
                Guid.NewGuid(), Calendarios.Tenant, Contratos.Cliente, 1, "Título", "   ", Prioridade.Baixa, OrigemSolicitacao.Portal,
                null, null, null, null, null, Abertura, Padrao());

            solicitacao.Descricao.Should().BeNull();
        }

        [Fact]
        public void ContatoVazio_LancaExcecao()
        {
            var acao = () => Solicitacao.Abrir(
                Guid.NewGuid(), Calendarios.Tenant, Contratos.Cliente, 1, "Título", null, Prioridade.Baixa, OrigemSolicitacao.Portal,
                Guid.Empty, null, null, null, null, Abertura, Padrao());

            acao.Should().Throw<ArgumentException>().WithParameterName("contatoId");
        }
    }

    /// <summary>Cada célula da tabela do §4.3: transições válidas levam ao destino; as demais lançam e não mudam o status.</summary>
    public sealed class MaquinaDeEstados
    {
        private static readonly Dictionary<(StatusSolicitacao, string), StatusSolicitacao> Validas = new()
        {
            [(StatusSolicitacao.Aberta, "Iniciar")] = StatusSolicitacao.EmAndamento,
            [(StatusSolicitacao.Aberta, "AguardarCliente")] = StatusSolicitacao.AguardandoCliente,
            [(StatusSolicitacao.Aberta, "AguardarTerceiro")] = StatusSolicitacao.AguardandoTerceiro,
            [(StatusSolicitacao.Aberta, "Resolver")] = StatusSolicitacao.Resolvida,
            [(StatusSolicitacao.Aberta, "Cancelar")] = StatusSolicitacao.Cancelada,
            [(StatusSolicitacao.EmAndamento, "AguardarCliente")] = StatusSolicitacao.AguardandoCliente,
            [(StatusSolicitacao.EmAndamento, "AguardarTerceiro")] = StatusSolicitacao.AguardandoTerceiro,
            [(StatusSolicitacao.EmAndamento, "Resolver")] = StatusSolicitacao.Resolvida,
            [(StatusSolicitacao.EmAndamento, "Cancelar")] = StatusSolicitacao.Cancelada,
            [(StatusSolicitacao.AguardandoCliente, "Retomar")] = StatusSolicitacao.EmAndamento,
            [(StatusSolicitacao.AguardandoCliente, "Resolver")] = StatusSolicitacao.Resolvida,
            [(StatusSolicitacao.AguardandoCliente, "Cancelar")] = StatusSolicitacao.Cancelada,
            [(StatusSolicitacao.AguardandoTerceiro, "Retomar")] = StatusSolicitacao.EmAndamento,
            [(StatusSolicitacao.AguardandoTerceiro, "Resolver")] = StatusSolicitacao.Resolvida,
            [(StatusSolicitacao.AguardandoTerceiro, "Cancelar")] = StatusSolicitacao.Cancelada,
            [(StatusSolicitacao.Resolvida, "Reabrir")] = StatusSolicitacao.EmAndamento,
            [(StatusSolicitacao.Resolvida, "Fechar")] = StatusSolicitacao.Fechada,
        };

        private static readonly string[] Operacoes =
            ["Iniciar", "AguardarCliente", "AguardarTerceiro", "Retomar", "Resolver", "Reabrir", "Fechar", "Cancelar"];

        public static TheoryData<StatusSolicitacao, string> TransicoesValidas()
        {
            var dados = new TheoryData<StatusSolicitacao, string>();
            foreach (var (origem, operacao) in Validas.Keys)
                dados.Add(origem, operacao);
            return dados;
        }

        public static TheoryData<StatusSolicitacao, string> TransicoesInvalidas()
        {
            var dados = new TheoryData<StatusSolicitacao, string>();
            foreach (var (origem, operacao) in CelulasInvalidas())
                dados.Add(origem, operacao);
            return dados;
        }

        [Theory]
        [MemberData(nameof(TransicoesValidas))]
        public void TransicaoDaTabela_LevaAoDestino(StatusSolicitacao origem, string operacao)
        {
            var solicitacao = Em(origem);

            Executar(solicitacao, operacao);

            solicitacao.Status.Should().Be(Validas[(origem, operacao)]);
        }

        [Theory]
        [MemberData(nameof(TransicoesValidas))]
        public void TransicaoDaTabela_EmiteStatusAlterado(StatusSolicitacao origem, string operacao)
        {
            var solicitacao = Em(origem);

            Executar(solicitacao, operacao);

            var evento = solicitacao.Eventos.OfType<SolicitacaoStatusAlterado>().Single();
            evento.De.Should().Be(origem);
            evento.Para.Should().Be(Validas[(origem, operacao)]);
            evento.ExternoDe.Should().Be(Solicitacao.ParaExterno(origem));
            evento.ExternoPara.Should().Be(Solicitacao.ParaExterno(Validas[(origem, operacao)]));
        }

        [Theory]
        [MemberData(nameof(TransicoesInvalidas))]
        public void ForaDaTabela_LancaExcecaoSemMudarStatus(StatusSolicitacao origem, string operacao)
        {
            var solicitacao = Em(origem);

            var acao = () => Executar(solicitacao, operacao);

            acao.Should().Throw<TransicaoInvalidaException>();
            solicitacao.Status.Should().Be(origem);
            solicitacao.Eventos.Should().BeEmpty();
        }

        [Fact]
        public void Matriz_CobreTodasAsCelulas()
        {
            (Validas.Count + CelulasInvalidas().Count())
                .Should().Be(Enum.GetValues<StatusSolicitacao>().Length * Operacoes.Length);
        }

        private static IEnumerable<(StatusSolicitacao Origem, string Operacao)> CelulasInvalidas() =>
            from origem in Enum.GetValues<StatusSolicitacao>()
            from operacao in Operacoes
            where !Validas.ContainsKey((origem, operacao))
            select (origem, operacao);

        private static void Executar(Solicitacao solicitacao, string operacao)
        {
            var agora = Brasilia(6, 11);
            switch (operacao)
            {
                case "Iniciar": solicitacao.Iniciar(agora); break;
                case "AguardarCliente": solicitacao.AguardarCliente(agora); break;
                case "AguardarTerceiro": solicitacao.AguardarTerceiro(agora); break;
                case "Retomar": solicitacao.Retomar(agora, Padrao()); break;
                case "Resolver": solicitacao.Resolver(Solucao(), agora, Padrao()); break;
                case "Reabrir": solicitacao.Reabrir("Cliente reprovou", agora, Padrao()); break;
                case "Fechar": solicitacao.Fechar(agora); break;
                case "Cancelar": solicitacao.Cancelar("Duplicada", agora); break;
                default: throw new ArgumentOutOfRangeException(nameof(operacao));
            }
        }
    }

    public sealed class StatusExternoDoPortal
    {
        [Theory]
        [InlineData(StatusSolicitacao.Aberta, StatusExterno.Recebida)]
        [InlineData(StatusSolicitacao.EmAndamento, StatusExterno.EmAndamento)]
        [InlineData(StatusSolicitacao.AguardandoTerceiro, StatusExterno.EmAndamento)]
        [InlineData(StatusSolicitacao.AguardandoCliente, StatusExterno.AguardandoVoce)]
        [InlineData(StatusSolicitacao.Resolvida, StatusExterno.Concluida)]
        [InlineData(StatusSolicitacao.Fechada, StatusExterno.Concluida)]
        [InlineData(StatusSolicitacao.Cancelada, StatusExterno.Cancelada)]
        public void CadaStatusInterno_TemSeuExterno(StatusSolicitacao interno, StatusExterno externo)
        {
            Em(interno).StatusExterno.Should().Be(externo);
        }

        [Fact]
        public void StatusInvalido_LancaExcecao()
        {
            var acao = () => Solicitacao.ParaExterno((StatusSolicitacao)99);

            acao.Should().Throw<ArgumentOutOfRangeException>();
        }
    }

    public sealed class RelogioNasTransicoes
    {
        [Fact]
        public void AguardarCliente_PausaORelogio()
        {
            var solicitacao = Em(StatusSolicitacao.EmAndamento);

            solicitacao.AguardarCliente(Brasilia(6, 11));

            solicitacao.Sla!.PausadoDesde.Should().Be(Brasilia(6, 11));
        }

        [Fact]
        public void AguardarTerceiro_NaoPausa()
        {
            var solicitacao = Em(StatusSolicitacao.EmAndamento);

            solicitacao.AguardarTerceiro(Brasilia(6, 11));

            solicitacao.Sla!.Pausado.Should().BeFalse();
            solicitacao.SituacaoSla(Brasilia(6, 14, 30), Padrao()).Should().Be(SituacaoSla.Violado);
        }

        [Fact]
        public void Retomar_DeAguardandoCliente_EmpurraOPrazo()
        {
            var solicitacao = Em(StatusSolicitacao.EmAndamento);
            solicitacao.AguardarCliente(Brasilia(6, 11));

            solicitacao.Retomar(Brasilia(6, 13), Padrao());

            solicitacao.Sla!.Pausado.Should().BeFalse();
            solicitacao.Sla.PrazoResolucao.Should().Be(Brasilia(6, 16));
        }

        [Fact]
        public void Retomar_DeAguardandoTerceiro_NaoMexeNoPrazo()
        {
            var solicitacao = Em(StatusSolicitacao.AguardandoTerceiro);

            solicitacao.Retomar(Brasilia(6, 13), Padrao());

            solicitacao.Sla!.PrazoResolucao.Should().Be(Brasilia(6, 14));
        }

        [Fact]
        public void Resolver_DuranteAPausa_EncerraAPausaAntesDeParar()
        {
            var solicitacao = Em(StatusSolicitacao.EmAndamento);
            solicitacao.AguardarCliente(Brasilia(6, 11));

            solicitacao.Resolver(Solucao(), Brasilia(6, 15), Padrao());

            solicitacao.Sla!.Pausado.Should().BeFalse();
            solicitacao.Sla.PrazoResolucao.Should().Be(Brasilia(6, 18));
            solicitacao.SituacaoSla(Brasilia(6, 15), Padrao()).Should().Be(SituacaoSla.Cumprido);
        }

        [Fact]
        public void Resolver_GuardaSolucaoEInstanteEEmiteEvento()
        {
            var solicitacao = Em(StatusSolicitacao.EmAndamento);

            solicitacao.Resolver(Solucao(), Brasilia(6, 12), Padrao());

            solicitacao.Solucao.Should().Be(Solucao());
            solicitacao.ResolvidaEm.Should().Be(Brasilia(6, 12));
            solicitacao.Eventos.Should().Contain(new SolicitacaoResolvida(solicitacao.Id, Calendarios.Tenant, Contratos.Cliente, Brasilia(6, 12)));
        }

        [Fact]
        public void Reabrir_NovoCicloELimpaSolucao()
        {
            var solicitacao = Em(StatusSolicitacao.Resolvida);

            solicitacao.Reabrir("Data ainda errada", Brasilia(8, 10), Padrao());

            solicitacao.Status.Should().Be(StatusSolicitacao.EmAndamento);
            solicitacao.Solucao.Should().BeNull();
            solicitacao.ResolvidaEm.Should().BeNull();
            solicitacao.Sla!.InicioCiclo.Should().Be(Brasilia(8, 10));
            solicitacao.Sla.PrazoResolucao.Should().Be(Brasilia(8, 14));
            solicitacao.Eventos.Should().Contain(
                new SolicitacaoReaberta(solicitacao.Id, Calendarios.Tenant, Contratos.Cliente, "Data ainda errada", Brasilia(8, 10)));
        }

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        public void Reabrir_SemMotivo_LancaExcecao(string motivoInformado)
        {
            var solicitacao = Em(StatusSolicitacao.Resolvida);

            var acao = () => solicitacao.Reabrir(motivoInformado, Brasilia(8, 10), Padrao());

            acao.Should().Throw<ArgumentException>().WithParameterName("motivo");
            solicitacao.Status.Should().Be(StatusSolicitacao.Resolvida);
        }

        [Fact]
        public void Fechar_GuardaInstante()
        {
            var solicitacao = Em(StatusSolicitacao.Resolvida);

            solicitacao.Fechar(Brasilia(13, 10));

            solicitacao.FechadaEm.Should().Be(Brasilia(13, 10));
        }

        [Fact]
        public void Cancelar_GuardaMotivoEInstante()
        {
            var solicitacao = Em(StatusSolicitacao.EmAndamento);

            solicitacao.Cancelar("  Aberta em duplicidade ", Brasilia(6, 12));

            solicitacao.MotivoCancelamento.Should().Be("Aberta em duplicidade");
            solicitacao.CanceladaEm.Should().Be(Brasilia(6, 12));
            solicitacao.SituacaoSla(Brasilia(6, 12), Padrao()).Should().Be(SituacaoSla.SemSla);
        }

        [Fact]
        public void Cancelar_SemMotivo_LancaExcecao()
        {
            var solicitacao = Em(StatusSolicitacao.EmAndamento);

            var acao = () => solicitacao.Cancelar(" ", Brasilia(6, 12));

            acao.Should().Throw<ArgumentException>();
            solicitacao.Status.Should().Be(StatusSolicitacao.EmAndamento);
        }

        [Fact]
        public void SemContrato_TransicoesFuncionamSemRelogio()
        {
            var solicitacao = Em(StatusSolicitacao.EmAndamento, comContrato: false);

            solicitacao.AguardarCliente(Brasilia(6, 11));
            solicitacao.Retomar(Brasilia(6, 12), Padrao());
            solicitacao.Resolver(Solucao(), Brasilia(6, 13), Padrao());
            solicitacao.Reabrir("Reprovada", Brasilia(6, 14), Padrao());

            solicitacao.Sla.Should().BeNull();
            solicitacao.Status.Should().Be(StatusSolicitacao.EmAndamento);
        }
    }

    public sealed class Repriorizar
    {
        [Fact]
        public void ComContrato_RecalculaPrazos()
        {
            var solicitacao = Em(StatusSolicitacao.EmAndamento);

            solicitacao.Repriorizar(Prioridade.Alta, Contratos.PoliticaPadrao(), Padrao());

            solicitacao.Prioridade.Should().Be(Prioridade.Alta);
            solicitacao.Sla!.PrazoPrimeiraResposta.Should().Be(Brasilia(6, 12));
            solicitacao.Sla.PrazoResolucao.Should().Be(Brasilia(7, 16));
        }

        [Fact]
        public void ComPrimeiraRespostaRegistrada_MantemPrazoDePrimeiraResposta()
        {
            var solicitacao = Em(StatusSolicitacao.EmAndamento);
            solicitacao.RegistrarPrimeiraResposta(Brasilia(6, 10, 40));

            solicitacao.Repriorizar(Prioridade.Alta, Contratos.PoliticaPadrao(), Padrao());

            solicitacao.Sla!.PrazoPrimeiraResposta.Should().Be(Brasilia(6, 11));
        }

        [Fact]
        public void SemContrato_SoMudaPrioridade()
        {
            var solicitacao = Em(StatusSolicitacao.Aberta, comContrato: false);

            solicitacao.Repriorizar(Prioridade.Baixa, null, Padrao());

            solicitacao.Prioridade.Should().Be(Prioridade.Baixa);
            solicitacao.Sla.Should().BeNull();
        }

        [Fact]
        public void ComContratoSemPolitica_LancaExcecao()
        {
            var solicitacao = Em(StatusSolicitacao.Aberta);

            var acao = () => solicitacao.Repriorizar(Prioridade.Baixa, null, Padrao());

            acao.Should().Throw<ArgumentException>();
            solicitacao.Prioridade.Should().Be(Prioridade.Urgente);
        }

        [Theory]
        [InlineData(StatusSolicitacao.Resolvida)]
        [InlineData(StatusSolicitacao.Fechada)]
        [InlineData(StatusSolicitacao.Cancelada)]
        public void ResolvidaOuFinal_LancaExcecao(StatusSolicitacao status)
        {
            var solicitacao = Em(status);

            var acao = () => solicitacao.Repriorizar(Prioridade.Baixa, Contratos.PoliticaPadrao(), Padrao());

            acao.Should().Throw<TransicaoInvalidaException>();
        }

        [Fact]
        public void DuranteAPausa_SituacaoContinuaCongelada()
        {
            var solicitacao = Em(StatusSolicitacao.EmAndamento);
            solicitacao.RegistrarPrimeiraResposta(Brasilia(6, 10, 40));
            solicitacao.AguardarCliente(Brasilia(6, 12));

            solicitacao.Repriorizar(Prioridade.Alta, Contratos.PoliticaPadrao(), Padrao());

            // Pausa às 12:00 com 2h de 16h consumidas; dias depois continua no prazo.
            solicitacao.Sla!.Pausado.Should().BeTrue();
            solicitacao.SituacaoSla(Brasilia(9, 17), Padrao()).Should().Be(SituacaoSla.NoPrazo);
        }
    }

    public sealed class PrimeiraResposta
    {
        [Fact]
        public void Registrar_GravaEEmiteEvento()
        {
            var solicitacao = Em(StatusSolicitacao.EmAndamento);

            solicitacao.RegistrarPrimeiraResposta(Brasilia(6, 10, 40));

            solicitacao.PrimeiraRespostaEm.Should().Be(Brasilia(6, 10, 40));
            solicitacao.Eventos.Should().ContainSingle();
            solicitacao.Eventos[0].Should().Be(
                new PrimeiraRespostaRegistrada(solicitacao.Id, Calendarios.Tenant, Contratos.Cliente, Brasilia(6, 10, 40)));
        }

        [Fact]
        public void Registrar_SegundaVez_NaoMudaNemEmite()
        {
            var solicitacao = Em(StatusSolicitacao.EmAndamento);
            solicitacao.RegistrarPrimeiraResposta(Brasilia(6, 10, 40));
            solicitacao.LimparEventos();

            solicitacao.RegistrarPrimeiraResposta(Brasilia(6, 12));

            solicitacao.PrimeiraRespostaEm.Should().Be(Brasilia(6, 10, 40));
            solicitacao.Eventos.Should().BeEmpty();
        }

        [Fact]
        public void Registrar_SemContrato_GravaMesmoAssim()
        {
            var solicitacao = Em(StatusSolicitacao.Aberta, comContrato: false);

            solicitacao.RegistrarPrimeiraResposta(Brasilia(6, 10, 40));

            solicitacao.PrimeiraRespostaEm.Should().Be(Brasilia(6, 10, 40));
        }

        [Theory]
        [InlineData(StatusSolicitacao.Fechada)]
        [InlineData(StatusSolicitacao.Cancelada)]
        public void Registrar_EmEstadoFinal_LancaExcecao(StatusSolicitacao status)
        {
            var solicitacao = Em(status);

            var acao = () => solicitacao.RegistrarPrimeiraResposta(Brasilia(6, 12));

            acao.Should().Throw<TransicaoInvalidaException>();
        }
    }

    public sealed class SituacaoDoSla
    {
        [Fact]
        public void EmAberto_MostraAPiorEntrePrimeiraRespostaEResolucao()
        {
            var solicitacao = Abrir();

            // 11:30: resolução em 37% (no prazo), primeira resposta vencida às 11:00.
            solicitacao.SituacaoSla(Brasilia(6, 11, 30), Padrao()).Should().Be(SituacaoSla.Violado);
        }

        [Fact]
        public void EmAberto_PrimeiraRespostaCumprida_SegueAResolucao()
        {
            var solicitacao = Abrir();
            solicitacao.RegistrarPrimeiraResposta(Brasilia(6, 10, 30));

            solicitacao.SituacaoSla(Brasilia(6, 11, 30), Padrao()).Should().Be(SituacaoSla.NoPrazo);
            solicitacao.SituacaoSla(Brasilia(6, 13, 30), Padrao()).Should().Be(SituacaoSla.EmRisco);
        }

        [Fact]
        public void ResolvidaNoPrazo_CumpridoMesmoDepois()
        {
            var solicitacao = Abrir();
            solicitacao.Resolver(Solucao(), Brasilia(6, 13), Padrao());
            solicitacao.Fechar(Brasilia(13, 10));

            solicitacao.SituacaoSla(Brasilia(20, 10), Padrao()).Should().Be(SituacaoSla.Cumprido);
        }

        [Fact]
        public void ResolvidaAtrasada_Violado()
        {
            var solicitacao = Abrir();

            solicitacao.Resolver(Solucao(), Brasilia(6, 15), Padrao());

            solicitacao.SituacaoSla(Brasilia(6, 15), Padrao()).Should().Be(SituacaoSla.Violado);
        }
    }

    public sealed class Edicao
    {
        [Fact]
        public void Editar_AlteraTituloEDescricao()
        {
            var solicitacao = Em(StatusSolicitacao.EmAndamento);

            solicitacao.Editar(" Novo título ", null);

            solicitacao.Titulo.Should().Be("Novo título");
            solicitacao.Descricao.Should().BeNull();
        }

        [Fact]
        public void Atribuir_EmiteEventoSoQuandoMuda()
        {
            var responsavel = Guid.NewGuid();
            var solicitacao = Em(StatusSolicitacao.Aberta);

            solicitacao.Atribuir(responsavel);
            solicitacao.Atribuir(responsavel);

            solicitacao.ResponsavelId.Should().Be(responsavel);
            solicitacao.Eventos.Should().ContainSingle();
            solicitacao.Eventos[0].Should().Be(new SolicitacaoAtribuida(solicitacao.Id, Calendarios.Tenant, Contratos.Cliente, responsavel));
        }

        [Fact]
        public void Atribuir_Nulo_RemoveResponsavel()
        {
            var solicitacao = Em(StatusSolicitacao.Aberta);
            solicitacao.Atribuir(Guid.NewGuid());

            solicitacao.Atribuir(null);

            solicitacao.ResponsavelId.Should().BeNull();
        }

        [Fact]
        public void DefinirCategoria_Altera()
        {
            var categoria = Guid.NewGuid();
            var solicitacao = Em(StatusSolicitacao.Resolvida);

            solicitacao.DefinirCategoria(categoria);

            solicitacao.CategoriaId.Should().Be(categoria);
        }

        [Theory]
        [InlineData(StatusSolicitacao.Fechada)]
        [InlineData(StatusSolicitacao.Cancelada)]
        public void EmEstadoFinal_EdicoesLancamExcecao(StatusSolicitacao status)
        {
            var solicitacao = Em(status);

            ((Action)(() => solicitacao.Editar("Outro", null))).Should().Throw<TransicaoInvalidaException>();
            ((Action)(() => solicitacao.Atribuir(Guid.NewGuid()))).Should().Throw<TransicaoInvalidaException>();
            ((Action)(() => solicitacao.DefinirCategoria(Guid.NewGuid()))).Should().Throw<TransicaoInvalidaException>();
        }
    }
}
