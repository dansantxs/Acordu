using System.Text.Json;
using Acordu.Application.Abstractions;
using Acordu.Application.Abstractions.Eventos;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;

namespace Acordu.Application.Atividades;

/// <summary>
/// Alimenta o feed de atividade (RF-COL-05) depois do commit, a partir dos eventos de solicitações e tarefas. Quem fez
/// vem do evento quando ele traz (abertura, interação, comentário) e, nos demais, da origem do evento (nulo = sistema).
/// Uma linha por mensagem da outbox.
/// </summary>
public sealed class RegistroDeAtividades :
    IManipuladorPosCommit<SolicitacaoAberta>,
    IManipuladorPosCommit<SolicitacaoAtribuida>,
    IManipuladorPosCommit<SolicitacaoStatusAlterado>,
    IManipuladorPosCommit<InteracaoRegistrada>,
    IManipuladorPosCommit<TarefaCriada>,
    IManipuladorPosCommit<TarefaAtribuida>,
    IManipuladorPosCommit<TarefaStatusAlterada>,
    IManipuladorPosCommit<TarefaProjetoAlterado>,
    IManipuladorPosCommit<ComentarioTarefaRegistrado>
{
    private readonly IAtividades _atividades;
    private readonly IOrigemDoEvento _origem;
    private readonly IRepositorio<Tarefa> _tarefas;

    public RegistroDeAtividades(IAtividades atividades, IOrigemDoEvento origem, IRepositorio<Tarefa> tarefas)
    {
        ArgumentNullException.ThrowIfNull(atividades);
        ArgumentNullException.ThrowIfNull(origem);
        ArgumentNullException.ThrowIfNull(tarefas);
        _atividades = atividades;
        _origem = origem;
        _tarefas = tarefas;
    }

    public Task TratarAsync(SolicitacaoAberta evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return GravarAsync(
            // Pelo portal ou por e-mail, quem abriu é o contato (F15-10).
            TipoAtividade.SolicitacaoAberta, evento.AbertaPor ?? _origem.AutorId, evento.Origem is OrigemSolicitacao.Portal or OrigemSolicitacao.Email,
            AlvoColaboracao.Solicitacao, evento.SolicitacaoId, evento.ClienteId, cancellationToken: cancellationToken);
    }

    public Task TratarAsync(SolicitacaoAtribuida evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return GravarAsync(
            TipoAtividade.SolicitacaoAtribuida, evento.AtribuidoPor ?? _origem.AutorId, false, AlvoColaboracao.Solicitacao,
            evento.SolicitacaoId, evento.ClienteId, responsavelId: evento.ResponsavelId, cancellationToken: cancellationToken);
    }

    public Task TratarAsync(SolicitacaoStatusAlterado evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return GravarAsync(
            TipoAtividade.SolicitacaoStatus, _origem.AutorId, false, AlvoColaboracao.Solicitacao, evento.SolicitacaoId, evento.ClienteId,
            Texto(evento.De), Texto(evento.Para), cancellationToken: cancellationToken);
    }

    public Task TratarAsync(InteracaoRegistrada evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        var tipo = evento.AutorEhCliente ? TipoAtividade.RespostaDoCliente
            : evento.Visibilidade == Visibilidade.Externa ? TipoAtividade.RespostaAoCliente
            : TipoAtividade.NotaInterna;
        return GravarAsync(
            tipo, evento.AutorId, evento.AutorEhCliente, AlvoColaboracao.Solicitacao, evento.SolicitacaoId, evento.ClienteId,
            cancellationToken: cancellationToken);
    }

    public async Task TratarAsync(TarefaCriada evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        await GravarAsync(
            TipoAtividade.TarefaCriada, _origem.AutorId, false, AlvoColaboracao.Tarefa, evento.TarefaId,
            await ClienteDaTarefaAsync(evento.TarefaId, cancellationToken), responsavelId: evento.ResponsavelId,
            cancellationToken: cancellationToken);
    }

    public async Task TratarAsync(TarefaAtribuida evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        await GravarAsync(
            TipoAtividade.TarefaAtribuida, _origem.AutorId, false, AlvoColaboracao.Tarefa, evento.TarefaId,
            await ClienteDaTarefaAsync(evento.TarefaId, cancellationToken), responsavelId: evento.ResponsavelId,
            cancellationToken: cancellationToken);
    }

    public async Task TratarAsync(TarefaStatusAlterada evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        await GravarAsync(
            TipoAtividade.TarefaStatus, _origem.AutorId, false, AlvoColaboracao.Tarefa, evento.TarefaId,
            await ClienteDaTarefaAsync(evento.TarefaId, cancellationToken), Texto(evento.De), Texto(evento.Para),
            cancellationToken: cancellationToken);
    }

    /// <summary>Os ids dos projetos vão nas colunas de/para da linha; a leitura troca pelos nomes atuais.</summary>
    public async Task TratarAsync(TarefaProjetoAlterado evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        await GravarAsync(
            TipoAtividade.TarefaProjeto, _origem.AutorId, false, AlvoColaboracao.Tarefa, evento.TarefaId,
            await ClienteDaTarefaAsync(evento.TarefaId, cancellationToken), evento.De?.ToString(), evento.Para?.ToString(),
            cancellationToken: cancellationToken);
    }

    public async Task TratarAsync(ComentarioTarefaRegistrado evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        await GravarAsync(
            TipoAtividade.ComentarioTarefa, evento.AutorId, false, AlvoColaboracao.Tarefa, evento.TarefaId,
            await ClienteDaTarefaAsync(evento.TarefaId, cancellationToken), cancellationToken: cancellationToken);
    }

    private static string Texto<T>(T valor)
        where T : struct, Enum => JsonNamingPolicy.SnakeCaseUpper.ConvertName(valor.ToString());

    private async Task<Guid?> ClienteDaTarefaAsync(Guid tarefaId, CancellationToken cancellationToken) =>
        (await _tarefas.ObterPorIdAsync(tarefaId, cancellationToken))?.ClienteId;

    private Task GravarAsync(
        TipoAtividade tipo,
        Guid? autorId,
        bool autorEhCliente,
        AlvoColaboracao alvo,
        Guid alvoId,
        Guid? clienteId,
        string? de = null,
        string? para = null,
        Guid? responsavelId = null,
        CancellationToken cancellationToken = default)
    {
        // Sem mensagem (chamada fora da outbox) não há como garantir a idempotência: não grava.
        if (_origem.MensagemId is not { } chave || _origem.OcorridoEm is not { } quando)
            return Task.CompletedTask;
        return _atividades.RegistrarAsync(
            new NovaAtividade(chave, tipo, quando, autorId, autorEhCliente, alvo, alvoId, clienteId, de, para, responsavelId),
            cancellationToken);
    }
}
