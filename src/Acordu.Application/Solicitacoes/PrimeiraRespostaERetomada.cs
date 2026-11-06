using Acordu.Application.Abstractions;
using Acordu.Application.Abstractions.Eventos;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;

namespace Acordu.Application.Solicitacoes;

/// <summary>
/// Regras do modelo §3.1, na mesma transação da interação: interação EXTERNA da equipe registra a primeira resposta
/// (idempotente) e, com a solicitação ainda ABERTA, a põe EM_ANDAMENTO (F11-05); resposta do cliente com a solicitação
/// em AGUARDANDO_CLIENTE a retoma (RF-POR-05). Solicitação já finalizada não muda: o comentário entra, a solicitação
/// fica como está.
/// </summary>
public sealed class PrimeiraRespostaERetomada : IManipuladorEvento<InteracaoRegistrada>
{
    private readonly IRepositorio<Solicitacao> _solicitacoes;
    private readonly ICalendarios _calendarios;

    public PrimeiraRespostaERetomada(IRepositorio<Solicitacao> solicitacoes, ICalendarios calendarios)
    {
        ArgumentNullException.ThrowIfNull(solicitacoes);
        ArgumentNullException.ThrowIfNull(calendarios);
        _solicitacoes = solicitacoes;
        _calendarios = calendarios;
    }

    public async Task TratarAsync(InteracaoRegistrada evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        if (evento.Visibilidade != Visibilidade.Externa)
            return;

        var solicitacao = await _solicitacoes.ObterPorIdAsync(evento.SolicitacaoId, cancellationToken);
        if (solicitacao is null || solicitacao.EstaFinalizada)
            return;

        if (!evento.AutorEhCliente)
        {
            solicitacao.RegistrarPrimeiraResposta(evento.Em);
            if (solicitacao.Status == StatusSolicitacao.Aberta)
                solicitacao.Iniciar(evento.Em);
        }
        else if (solicitacao.Status == StatusSolicitacao.AguardandoCliente)
            solicitacao.Retomar(evento.Em, await _calendarios.DoTenantAsync(cancellationToken));
    }
}
