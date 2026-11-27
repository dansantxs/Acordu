using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Application.Contratos;
using Acordu.Application.Solicitacoes;
using Acordu.Application.UnitTests.Exemplos;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.UnitTests.Solicitacoes;

/// <summary>
/// Spec do F15, F15f (F15-10; design §1.2): o núcleo da abertura pelo contato é um só, para o portal (origem PORTAL) e para
/// a entrada por e-mail (origem EMAIL): o próximo protocolo, o contrato como no portal (o único ATIVO vigente; senão sem
/// SLA), sem responsável, o contato como contato da solicitação e os anexos como a primeira mensagem EXTERNA do contato.
/// </summary>
public sealed class AberturaPeloContatoTests
{
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly Guid Cliente = Guid.NewGuid();
    private static readonly Guid Contato = Guid.NewGuid();
    private static readonly Guid Autor = Guid.NewGuid();

    private readonly Repositorio<Solicitacao> _solicitacoes = new();
    private readonly Repositorio<Contrato> _contratos = new();
    private readonly Repositorio<Interacao> _interacoes = new();

    private AberturaPeloContato Abertura(params Guid[] vigentes)
    {
        var calendarios = new CalendariosFixos(Empresa);
        return new AberturaPeloContato(
            _solicitacoes, _contratos, _interacoes, new ContratosVigentes(vigentes), new Protocolo(42), calendarios,
            new RelogioDoTenant(calendarios, new RelogioFixo(Falsos.Agora)));
    }

    private Contrato NovoContrato()
    {
        var contrato = Contrato.Criar(
            Guid.NewGuid(), Empresa, Cliente, TipoContrato.BancoHoras, new Periodo(new DateOnly(2026, 1, 1)), Duracao.FromHoras(20),
            PoliticaSla.Sugerida);
        _contratos.Adicionar(contrato);
        return contrato;
    }

    private static AberturaDoContato Dados(OrigemSolicitacao origem, CanalMensagem canal, IReadOnlyList<NovoAnexo>? anexos = null) =>
        new(Guid.NewGuid(), Empresa, Cliente, Contato, Autor, "Forno parou", "Descrição", Prioridade.Media, origem, canal, anexos ?? []);

    [Theory] // a mesma abertura para o portal e para o e-mail
    [InlineData(OrigemSolicitacao.Portal, CanalMensagem.Portal)]
    [InlineData(OrigemSolicitacao.Email, CanalMensagem.Email)]
    public async Task AbrirAsync_SemContratoNemAnexos_ProximoProtocoloSemResponsavelSemSlaESemMensagem(
        OrigemSolicitacao origem, CanalMensagem canal)
    {
        var solicitacao = await Abertura().AbrirAsync(Dados(origem, canal));

        _solicitacoes.Itens.Should().ContainSingle().Which.Should().BeSameAs(solicitacao);
        solicitacao.Numero.Should().Be(42);
        solicitacao.Origem.Should().Be(origem);
        solicitacao.Status.Should().Be(StatusSolicitacao.Aberta);
        solicitacao.TenantId.Should().Be(Empresa);
        solicitacao.ClienteId.Should().Be(Cliente);
        solicitacao.ContatoId.Should().Be(Contato);
        solicitacao.ResponsavelId.Should().BeNull();
        solicitacao.ContratoId.Should().BeNull();
        solicitacao.Sla.Should().BeNull("sem contrato, sem SLA");
        _interacoes.Itens.Should().BeEmpty("sem anexos, nenhuma mensagem");
    }

    [Fact] // F15-10: o único ATIVO vigente do cliente
    public async Task AbrirAsync_UmContratoVigente_SolicitacaoComOContratoEOSla()
    {
        var contrato = NovoContrato();

        var solicitacao = await Abertura(contrato.Id).AbrirAsync(Dados(OrigemSolicitacao.Email, CanalMensagem.Email));

        solicitacao.ContratoId.Should().Be(contrato.Id);
        solicitacao.Sla.Should().NotBeNull("o SLA do contrato vale desde a abertura");
    }

    [Fact] // F15-10: dois vigentes não dizem qual vale, como no portal
    public async Task AbrirAsync_DoisContratosVigentes_SemContratoNemSla()
    {
        var solicitacao = await Abertura(NovoContrato().Id, NovoContrato().Id).AbrirAsync(Dados(OrigemSolicitacao.Portal, CanalMensagem.Portal));

        solicitacao.ContratoId.Should().BeNull();
        solicitacao.Sla.Should().BeNull();
    }

    [Theory] // os anexos são a primeira mensagem EXTERNA do contato, no canal da entrada
    [InlineData(OrigemSolicitacao.Portal, CanalMensagem.Portal)]
    [InlineData(OrigemSolicitacao.Email, CanalMensagem.Email)]
    public async Task AbrirAsync_ComAnexos_PrimeiraMensagemExternaDoContatoNoCanalDaEntrada(OrigemSolicitacao origem, CanalMensagem canal)
    {
        NovoAnexo[] anexos = [new("logo.png", "image/png", 1024, "tenants/t/logo.png")];

        var solicitacao = await Abertura().AbrirAsync(Dados(origem, canal, anexos));

        var mensagem = _interacoes.Itens.Should().ContainSingle().Subject;
        mensagem.SolicitacaoId.Should().Be(solicitacao.Id);
        mensagem.AutorId.Should().Be(Autor);
        mensagem.AutorEhCliente.Should().BeTrue();
        mensagem.Visibilidade.Should().Be(Visibilidade.Externa);
        mensagem.Canal.Should().Be(canal);
        mensagem.Texto.Should().BeNull("a descrição já está na solicitação");
    }

    private sealed class Protocolo(int numero) : IGeradorProtocolo
    {
        public Task<int> ProximoNumeroSolicitacaoAsync(CancellationToken cancellationToken = default) => Task.FromResult(numero);
    }

    /// <summary>Os contratos vigentes do cliente são os ids dados.</summary>
    private sealed class ContratosVigentes(Guid[] vigentes) : IConsultasContrato
    {
        public Task<IReadOnlyList<Guid>> VigentesDoClienteAsync(Guid clienteId, DateOnly hoje, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(vigentes);

        public Task<ConsumoContratoDto?> ConsumoAsync(
            Guid contratoId, MesReferencia periodo, DateOnly hoje, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ResultadoPaginado<ContratoResumoDto>> ListarAsync(
            Guid? clienteId, StatusContrato? status, Paginacao paginacao, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ContratoDto?> DetalharAsync(Guid id, DateOnly hoje, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> ExisteAtivoSobrepostoAsync(
            Guid clienteId, TipoContrato tipo, Periodo vigencia, Guid? ignorar, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
