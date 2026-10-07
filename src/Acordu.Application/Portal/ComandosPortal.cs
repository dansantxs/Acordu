using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Application.Contratos;
using Acordu.Application.Solicitacoes;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;

namespace Acordu.Application.Portal;

/// <summary>
/// Escritas do contato no portal (contrato §12): abrir solicitação, responder (sempre EXTERNA; a resposta retoma a
/// solicitação que aguardava o cliente, §3.1), aprovar e reprovar a entrega. Solicitação fora do escopo → 404.
/// </summary>
public sealed class ComandosPortal
{
    private readonly ITenantContext _contexto;
    private readonly IPortalAtual _portal;
    private readonly IRepositorio<Solicitacao> _solicitacoes;
    private readonly IRepositorio<Cliente> _clientes;
    private readonly IRepositorio<Contrato> _contratos;
    private readonly IRepositorio<Interacao> _interacoes;
    private readonly IConsultasContrato _consultasContrato;
    private readonly IGeradorProtocolo _protocolo;
    private readonly ICalendarios _calendarios;
    private readonly RelogioDoTenant _relogio;
    private readonly IUnitOfWork _unidade;

    public ComandosPortal(
        ITenantContext contexto,
        IPortalAtual portal,
        IRepositorio<Solicitacao> solicitacoes,
        IRepositorio<Cliente> clientes,
        IRepositorio<Contrato> contratos,
        IRepositorio<Interacao> interacoes,
        IConsultasContrato consultasContrato,
        IGeradorProtocolo protocolo,
        ICalendarios calendarios,
        RelogioDoTenant relogio,
        IUnitOfWork unidade)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(portal);
        ArgumentNullException.ThrowIfNull(solicitacoes);
        ArgumentNullException.ThrowIfNull(clientes);
        ArgumentNullException.ThrowIfNull(contratos);
        ArgumentNullException.ThrowIfNull(interacoes);
        ArgumentNullException.ThrowIfNull(consultasContrato);
        ArgumentNullException.ThrowIfNull(protocolo);
        ArgumentNullException.ThrowIfNull(calendarios);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(unidade);
        _contexto = contexto;
        _portal = portal;
        _solicitacoes = solicitacoes;
        _clientes = clientes;
        _contratos = contratos;
        _interacoes = interacoes;
        _consultasContrato = consultasContrato;
        _protocolo = protocolo;
        _calendarios = calendarios;
        _relogio = relogio;
        _unidade = unidade;
    }

    /// <summary>
    /// Origem PORTAL, com o contato logado como contato da solicitação (é o que o escopo PROPRIAS usa). Contrato: o único
    /// ATIVO vigente do cliente, se houver exatamente um; senão fica sem SLA, como na abertura interna sem contrato.
    /// </summary>
    public async Task<Guid> AbrirSolicitacaoAsync(NovaSolicitacaoPortalRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        _contexto.Exigir(Permissoes.PortalSolicitacaoCriar);
        var contato = await _portal.ContatoAsync(cancellationToken);
        var cliente = await _clientes.ObterPorIdAsync(contato.ClienteId, cancellationToken);
        if (cliente is null || !cliente.Ativo)
            throw new AcessoNegadoException("Acesso ao portal removido.");

        var hoje = await _relogio.HojeAsync(cancellationToken);
        var vigentes = await _consultasContrato.VigentesDoClienteAsync(cliente.Id, hoje, cancellationToken);
        var contrato = vigentes.Count == 1 ? await _contratos.ObterPorIdAsync(vigentes[0], cancellationToken) : null;
        var calendario = await _calendarios.DoTenantAsync(cancellationToken);
        var numero = await _protocolo.ProximoNumeroSolicitacaoAsync(cancellationToken);

        var solicitacao = Solicitacao.Abrir(
            Guid.CreateVersion7(),
            cliente.TenantId,
            cliente.Id,
            numero,
            dados.Titulo,
            dados.Descricao,
            dados.Prioridade ?? Prioridade.Media,
            OrigemSolicitacao.Portal,
            contato.ContatoId,
            null,
            null,
            contrato?.Id,
            contrato?.Sla,
            _relogio.Agora,
            calendario);
        _solicitacoes.Adicionar(solicitacao);
        await _unidade.SalvarAsync(cancellationToken);
        return solicitacao.Id;
    }

    /// <summary>
    /// Resposta do cliente, sempre EXTERNA. Devolve o Id da interação. Autor: o usuário do contato ou, na sessão de link
    /// (sem conta), o Id do próprio contato.
    /// </summary>
    public async Task<Guid> ResponderAsync(Guid solicitacaoId, RespostaPortalRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var solicitacao = await CarregarAsync(solicitacaoId, Permissoes.PortalSolicitacaoComentar, cancellationToken);
        var contato = await _portal.ContatoAsync(cancellationToken);
        var interacao = Interacao.Registrar(
            Guid.CreateVersion7(),
            solicitacao.TenantId,
            solicitacao.ClienteId,
            solicitacao.Id,
            // Sessão de link não tem usuário: o autor é o contato (os nomes caem no contato do cliente).
            _contexto.UsuarioId ?? contato.ContatoId,
            autorEhCliente: true,
            dados.Texto,
            Visibilidade.Externa,
            [],
            ComandosSolicitacao.PoliticaPadrao,
            _relogio.Agora);
        _interacoes.Adicionar(interacao);
        await _unidade.SalvarAsync(cancellationToken);
        return interacao.Id;
    }

    /// <summary>Aprova a entrega: RESOLVIDA → FECHADA.</summary>
    public async Task AprovarAsync(Guid solicitacaoId, CancellationToken cancellationToken = default)
    {
        var solicitacao = await CarregarAsync(solicitacaoId, Permissoes.PortalSolicitacaoAprovar, cancellationToken);
        ExigirAguardandoAprovacao(solicitacao);
        solicitacao.Fechar(_relogio.Agora);
        await _unidade.SalvarAsync(cancellationToken);
    }

    /// <summary>Reprova a entrega com motivo: RESOLVIDA → EM_ANDAMENTO, com novo ciclo de SLA.</summary>
    public async Task ReprovarAsync(Guid solicitacaoId, ReprovacaoPortalRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var solicitacao = await CarregarAsync(solicitacaoId, Permissoes.PortalSolicitacaoAprovar, cancellationToken);
        ExigirAguardandoAprovacao(solicitacao);
        if (string.IsNullOrWhiteSpace(dados.Motivo))
            throw ErroDeCampo.Criar("motivo", "Conte o que ficou faltando na entrega.");
        solicitacao.Reabrir(dados.Motivo, _relogio.Agora, await _calendarios.DoTenantAsync(cancellationToken));
        await _unidade.SalvarAsync(cancellationToken);
    }

    private static void ExigirAguardandoAprovacao(Solicitacao solicitacao)
    {
        if (solicitacao.Status != StatusSolicitacao.Resolvida)
            throw new TransicaoInvalidaException("A solicitação não está aguardando a sua aprovação.");
    }

    /// <summary>Pelos filtros do contato e pela regra de visibilidade (escopo ou link); fora disso, 404.</summary>
    private async Task<Solicitacao> CarregarAsync(Guid id, string permissao, CancellationToken cancellationToken)
    {
        _contexto.Exigir(permissao);
        var contato = await _portal.ContatoAsync(cancellationToken);
        var solicitacao = await _solicitacoes.ObterPorIdAsync(id, cancellationToken);
        if (solicitacao is null || !contato.Ve(solicitacao.Id, solicitacao.ContatoId))
            throw new NaoEncontradoException("Solicitação não encontrada.");
        return solicitacao;
    }
}
