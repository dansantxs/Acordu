using Acordu.Application.Abstractions;
using Acordu.Application.Abstractions.Eventos;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;

namespace Acordu.Application.Colaboracao;

/// <summary>
/// Seguimento automático (RF-COL-03), depois do commit: quem cria, o responsável, quem comenta e quem é mencionado
/// passam a seguir a solicitação ou a tarefa. Só pessoas que ainda são da equipe; contato do cliente nunca segue.
/// Idempotente: seguir de novo não muda nada, e deixar de seguir vale até o próximo gatilho.
/// </summary>
public sealed class SeguimentoAutomatico :
    IManipuladorPosCommit<SolicitacaoAberta>,
    IManipuladorPosCommit<SolicitacaoAtribuida>,
    IManipuladorPosCommit<InteracaoRegistrada>,
    IManipuladorPosCommit<PessoasMencionadas>,
    IManipuladorPosCommit<TarefaCriada>,
    IManipuladorPosCommit<TarefaAtribuida>,
    IManipuladorPosCommit<ComentarioTarefaRegistrado>
{
    private readonly ISeguidores _seguidores;
    private readonly IPessoasDaEquipe _pessoas;
    private readonly IRepositorio<Solicitacao> _solicitacoes;
    private readonly IOrigemDoEvento _origem;

    public SeguimentoAutomatico(
        ISeguidores seguidores, IPessoasDaEquipe pessoas, IRepositorio<Solicitacao> solicitacoes, IOrigemDoEvento origem)
    {
        ArgumentNullException.ThrowIfNull(seguidores);
        ArgumentNullException.ThrowIfNull(pessoas);
        ArgumentNullException.ThrowIfNull(solicitacoes);
        ArgumentNullException.ThrowIfNull(origem);
        _seguidores = seguidores;
        _pessoas = pessoas;
        _solicitacoes = solicitacoes;
        _origem = origem;
    }

    public async Task TratarAsync(SolicitacaoAberta evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        var solicitacao = await _solicitacoes.ObterPorIdAsync(evento.SolicitacaoId, cancellationToken);
        var quem = evento.Origem == OrigemSolicitacao.Portal ? null : evento.AbertaPor;
        await SeguirAsync(AlvoColaboracao.Solicitacao, evento.SolicitacaoId, [quem, solicitacao?.ResponsavelId], cancellationToken);
    }

    public Task TratarAsync(SolicitacaoAtribuida evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return SeguirAsync(AlvoColaboracao.Solicitacao, evento.SolicitacaoId, [evento.ResponsavelId], cancellationToken);
    }

    public Task TratarAsync(InteracaoRegistrada evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return evento.AutorEhCliente
            ? Task.CompletedTask
            : SeguirAsync(AlvoColaboracao.Solicitacao, evento.SolicitacaoId, [evento.AutorId], cancellationToken);
    }

    public Task TratarAsync(PessoasMencionadas evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return SeguirAsync(evento.Alvo, evento.AlvoId, [.. evento.Mencionados.Select(id => (Guid?)id)], cancellationToken);
    }

    /// <summary>O autor vem da origem do evento (nulo na recorrência); o responsável, do próprio evento.</summary>
    public Task TratarAsync(TarefaCriada evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return SeguirAsync(AlvoColaboracao.Tarefa, evento.TarefaId, [_origem.AutorId, evento.ResponsavelId], cancellationToken);
    }

    public Task TratarAsync(TarefaAtribuida evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return SeguirAsync(AlvoColaboracao.Tarefa, evento.TarefaId, [evento.ResponsavelId], cancellationToken);
    }

    public Task TratarAsync(ComentarioTarefaRegistrado evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return SeguirAsync(AlvoColaboracao.Tarefa, evento.TarefaId, [evento.AutorId], cancellationToken);
    }

    private async Task SeguirAsync(AlvoColaboracao alvo, Guid alvoId, IEnumerable<Guid?> pessoas, CancellationToken cancellationToken)
    {
        foreach (var id in pessoas.OfType<Guid>().Distinct())
        {
            if (await _pessoas.ObterAsync(id, cancellationToken) is not null)
                await _seguidores.SeguirAsync(alvo, alvoId, id, cancellationToken);
        }
    }
}
