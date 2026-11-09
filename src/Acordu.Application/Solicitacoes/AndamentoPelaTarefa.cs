using Acordu.Application.Abstractions;
using Acordu.Application.Abstractions.Eventos;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;

namespace Acordu.Application.Solicitacoes;

/// <summary>
/// Trabalho começou, a solicitação sai de ABERTA (F11-18, F11-19): tarefa criada nela, ou tarefa dela que passa a
/// FAZENDO (inclusive pelo cronômetro). Na mesma transação (modelo §3.1); outro status não muda.
/// </summary>
public sealed class AndamentoPelaTarefa : IManipuladorEvento<TarefaCriada>, IManipuladorEvento<TarefaStatusAlterada>
{
    private readonly IRepositorio<Solicitacao> _solicitacoes;
    private readonly RelogioDoTenant _relogio;

    public AndamentoPelaTarefa(IRepositorio<Solicitacao> solicitacoes, RelogioDoTenant relogio)
    {
        ArgumentNullException.ThrowIfNull(solicitacoes);
        ArgumentNullException.ThrowIfNull(relogio);
        _solicitacoes = solicitacoes;
        _relogio = relogio;
    }

    public Task TratarAsync(TarefaCriada evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return IniciarAsync(evento.SolicitacaoId, cancellationToken);
    }

    public Task TratarAsync(TarefaStatusAlterada evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return evento.Para == StatusTarefa.Fazendo ? IniciarAsync(evento.SolicitacaoId, cancellationToken) : Task.CompletedTask;
    }

    private async Task IniciarAsync(Guid? solicitacaoId, CancellationToken cancellationToken)
    {
        if (solicitacaoId is not { } id)
            return;
        var solicitacao = await _solicitacoes.ObterPorIdAsync(id, cancellationToken);
        if (solicitacao?.Status == StatusSolicitacao.Aberta)
            solicitacao.Iniciar(_relogio.Agora);
    }
}
