using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Application.Contratos;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Solicitacoes;

/// <summary>
/// O núcleo da abertura pelo contato (design do F15, §1.2, "o contato fala"), o mesmo para o portal (origem PORTAL) e para
/// a entrada por e-mail (origem EMAIL, F15-10): o próximo protocolo; o contrato como no portal (o único ATIVO vigente do
/// cliente; senão sem SLA); sem responsável nem categoria; o contato como contato da solicitação (é o que o escopo
/// PROPRIAS do portal usa, F15-28); e os anexos como a primeira mensagem EXTERNA do contato, sem texto, no canal da entrada.
/// Só acrescenta à unidade de trabalho: quem chama grava, com o que mais for da mesma transação.
/// </summary>
public sealed class AberturaPeloContato
{
    private readonly IRepositorio<Solicitacao> _solicitacoes;
    private readonly IRepositorio<Contrato> _contratos;
    private readonly IRepositorio<Interacao> _interacoes;
    private readonly IConsultasContrato _consultasContrato;
    private readonly IGeradorProtocolo _protocolo;
    private readonly ICalendarios _calendarios;
    private readonly RelogioDoTenant _relogio;

    public AberturaPeloContato(
        IRepositorio<Solicitacao> solicitacoes,
        IRepositorio<Contrato> contratos,
        IRepositorio<Interacao> interacoes,
        IConsultasContrato consultasContrato,
        IGeradorProtocolo protocolo,
        ICalendarios calendarios,
        RelogioDoTenant relogio)
    {
        ArgumentNullException.ThrowIfNull(solicitacoes);
        ArgumentNullException.ThrowIfNull(contratos);
        ArgumentNullException.ThrowIfNull(interacoes);
        ArgumentNullException.ThrowIfNull(consultasContrato);
        ArgumentNullException.ThrowIfNull(protocolo);
        ArgumentNullException.ThrowIfNull(calendarios);
        ArgumentNullException.ThrowIfNull(relogio);
        _solicitacoes = solicitacoes;
        _contratos = contratos;
        _interacoes = interacoes;
        _consultasContrato = consultasContrato;
        _protocolo = protocolo;
        _calendarios = calendarios;
        _relogio = relogio;
    }

    /// <summary>A solicitação aberta (e a interação dos anexos, se houver), acrescentadas sem gravar.</summary>
    public async Task<Solicitacao> AbrirAsync(AberturaDoContato dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var hoje = await _relogio.HojeAsync(cancellationToken);
        var vigentes = await _consultasContrato.VigentesDoClienteAsync(dados.ClienteId, hoje, cancellationToken);
        var contrato = vigentes.Count == 1 ? await _contratos.ObterPorIdAsync(vigentes[0], cancellationToken) : null;
        var calendario = await _calendarios.DoTenantAsync(cancellationToken);
        var numero = await _protocolo.ProximoNumeroSolicitacaoAsync(cancellationToken);
        var solicitacao = Solicitacao.Abrir(
            dados.SolicitacaoId,
            dados.TenantId,
            dados.ClienteId,
            numero,
            dados.Titulo,
            dados.Descricao,
            dados.Prioridade,
            dados.Origem,
            dados.ContatoId,
            null,
            null,
            contrato?.Id,
            contrato?.Sla,
            _relogio.Agora,
            calendario);
        _solicitacoes.Adicionar(solicitacao);
        if (dados.Anexos.Count > 0)
        {
            _interacoes.Adicionar(Interacao.Registrar(
                Guid.CreateVersion7(),
                solicitacao.TenantId,
                solicitacao.ClienteId,
                solicitacao.Id,
                dados.AutorId,
                autorEhCliente: true,
                null,
                Visibilidade.Externa,
                dados.Canal,
                dados.Anexos,
                ComandosSolicitacao.PoliticaPadrao,
                _relogio.Agora));
        }

        return solicitacao;
    }
}

/// <param name="SolicitacaoId">Sorteado antes, quando os anexos já subiram com a chave da solicitação.</param>
/// <param name="AutorId">O autor da mensagem dos anexos: a conta do contato ou, sem ela, o próprio contato.</param>
/// <param name="Canal">Por onde o contato falou (PORTAL ou EMAIL; RF-SOL-14).</param>
/// <param name="Anexos">Já no storage; vazio = sem a mensagem dos anexos.</param>
public sealed record AberturaDoContato(
    Guid SolicitacaoId,
    Guid TenantId,
    Guid ClienteId,
    Guid ContatoId,
    Guid AutorId,
    string Titulo,
    string? Descricao,
    Prioridade Prioridade,
    OrigemSolicitacao Origem,
    CanalMensagem Canal,
    IReadOnlyList<NovoAnexo> Anexos);
