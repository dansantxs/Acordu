using Acordu.Application.Abstractions;
using Acordu.Application.Abstractions.Eventos;
using Acordu.Domain.Events;

namespace Acordu.Application.Auditoria;

/// <summary>
/// Mudança de status e de responsável da solicitação (segurança §8) pelos eventos, dentro da transação: o status muda
/// por vários caminhos (equipe, portal, retomada pela resposta do contato, fechamento automático) e todos passam aqui.
/// Quem agiu vem do contexto: nulo no job e na sessão de link.
/// </summary>
public sealed class AuditoriaDeSolicitacoes :
    IManipuladorEvento<SolicitacaoStatusAlterado>,
    IManipuladorEvento<SolicitacaoAtribuida>,
    IManipuladorEvento<SolicitacaoContratoAlterado>
{
    private readonly IAuditoria _auditoria;

    public AuditoriaDeSolicitacoes(IAuditoria auditoria)
    {
        ArgumentNullException.ThrowIfNull(auditoria);
        _auditoria = auditoria;
    }

    public Task TratarAsync(SolicitacaoStatusAlterado evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        _auditoria.Registrar(AcoesAuditoria.SolicitacaoStatus, "solicitacao", evento.SolicitacaoId, new { de = evento.De, para = evento.Para });
        return Task.CompletedTask;
    }

    public Task TratarAsync(SolicitacaoAtribuida evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        _auditoria.Registrar(
            AcoesAuditoria.SolicitacaoResponsavel, "solicitacao", evento.SolicitacaoId, new { de = evento.Anterior, para = evento.ResponsavelId });
        return Task.CompletedTask;
    }

    public Task TratarAsync(SolicitacaoContratoAlterado evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        _auditoria.Registrar(
            AcoesAuditoria.SolicitacaoContrato, "solicitacao", evento.SolicitacaoId, new { de = evento.Anterior, para = evento.ContratoId });
        return Task.CompletedTask;
    }
}
