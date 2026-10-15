using Acordu.Application.Abstractions;
using Acordu.Application.Abstractions.Eventos;
using Acordu.Application.Colaboracao;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;
using Acordu.Domain.Services;

namespace Acordu.Application.Notificacoes;

/// <summary>
/// Avisos a quem segue (RF-COL-03, categoria SEGUIDAS), depois do commit: nova interação na solicitação, novo comentário
/// na tarefa e mudança de status. Nunca para quem causou o evento, nem para quem foi mencionado no mesmo texto (esse
/// recebe a MENCAO), nem para quem perdeu a permissão de ver o alvo.
/// </summary>
public sealed class AvisosAosSeguidores :
    IManipuladorPosCommit<InteracaoRegistrada>,
    IManipuladorPosCommit<ComentarioTarefaRegistrado>,
    IManipuladorPosCommit<SolicitacaoStatusAlterado>,
    IManipuladorPosCommit<TarefaStatusAlterada>
{
    private readonly ISeguidores _seguidores;
    private readonly IPessoasDaEquipe _pessoas;
    private readonly IRepositorio<Solicitacao> _solicitacoes;
    private readonly IRepositorio<Interacao> _interacoes;
    private readonly IRepositorio<Tarefa> _tarefas;
    private readonly IRepositorio<ComentarioTarefa> _comentarios;
    private readonly IOrigemDoEvento _origem;
    private readonly AvisoInterno _aviso;
    private readonly EnderecosPublicos _enderecos;

    public AvisosAosSeguidores(
        ISeguidores seguidores,
        IPessoasDaEquipe pessoas,
        IRepositorio<Solicitacao> solicitacoes,
        IRepositorio<Interacao> interacoes,
        IRepositorio<Tarefa> tarefas,
        IRepositorio<ComentarioTarefa> comentarios,
        IOrigemDoEvento origem,
        AvisoInterno aviso,
        EnderecosPublicos enderecos)
    {
        ArgumentNullException.ThrowIfNull(seguidores);
        ArgumentNullException.ThrowIfNull(pessoas);
        ArgumentNullException.ThrowIfNull(solicitacoes);
        ArgumentNullException.ThrowIfNull(interacoes);
        ArgumentNullException.ThrowIfNull(tarefas);
        ArgumentNullException.ThrowIfNull(comentarios);
        ArgumentNullException.ThrowIfNull(origem);
        ArgumentNullException.ThrowIfNull(aviso);
        ArgumentNullException.ThrowIfNull(enderecos);
        _seguidores = seguidores;
        _pessoas = pessoas;
        _solicitacoes = solicitacoes;
        _interacoes = interacoes;
        _tarefas = tarefas;
        _comentarios = comentarios;
        _origem = origem;
        _aviso = aviso;
        _enderecos = enderecos;
    }

    public async Task TratarAsync(InteracaoRegistrada evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        var solicitacao = await _solicitacoes.ObterPorIdAsync(evento.SolicitacaoId, cancellationToken);
        var interacao = await _interacoes.ObterPorIdAsync(evento.InteracaoId, cancellationToken);
        if (solicitacao is null || interacao is null)
            return;

        var quem = evento.AutorEhCliente ? "O cliente" : await NomeAsync(evento.AutorId, cancellationToken);
        var verbo = evento.AutorEhCliente ? "respondeu" : evento.Visibilidade == Visibilidade.Interna ? "deixou uma nota interna" : "respondeu ao cliente";
        IReadOnlyList<Guid> mencionados = evento.AutorEhCliente ? [] : Mencoes.Extrair(interacao.Texto, evento.AutorId);
        await AvisarAsync(
            AlvoColaboracao.Solicitacao,
            solicitacao.Id,
            [evento.AutorId, .. mencionados],
            $"seguidas:interacao:{interacao.Id}",
            $"{quem} {verbo} em #{solicitacao.Numero} {solicitacao.Titulo}",
            _enderecos.SolicitacaoInterna(solicitacao.Id),
            cancellationToken);
    }

    public async Task TratarAsync(ComentarioTarefaRegistrado evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        var tarefa = await _tarefas.ObterPorIdAsync(evento.TarefaId, cancellationToken);
        var comentario = await _comentarios.ObterPorIdAsync(evento.ComentarioId, cancellationToken);
        if (tarefa is null || comentario is null)
            return;

        await AvisarAsync(
            AlvoColaboracao.Tarefa,
            tarefa.Id,
            [evento.AutorId, .. Mencoes.Extrair(comentario.Texto, evento.AutorId)],
            $"seguidas:comentario:{comentario.Id}",
            $"{await NomeAsync(evento.AutorId, cancellationToken)} comentou na tarefa {tarefa.Titulo}",
            _enderecos.TarefaInterna(tarefa.Id),
            cancellationToken);
    }

    public async Task TratarAsync(SolicitacaoStatusAlterado evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        if (await _solicitacoes.ObterPorIdAsync(evento.SolicitacaoId, cancellationToken) is not { } solicitacao)
            return;

        await AvisarAsync(
            AlvoColaboracao.Solicitacao,
            solicitacao.Id,
            [_origem.AutorId],
            $"seguidas:status:{solicitacao.Id}:{evento.Para}:{evento.Em.UtcTicks}",
            $"#{solicitacao.Numero} {solicitacao.Titulo} agora está {Textos.Status(evento.Para)}",
            _enderecos.SolicitacaoInterna(solicitacao.Id),
            cancellationToken);
    }

    public async Task TratarAsync(TarefaStatusAlterada evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        if (await _tarefas.ObterPorIdAsync(evento.TarefaId, cancellationToken) is not { } tarefa)
            return;

        await AvisarAsync(
            AlvoColaboracao.Tarefa,
            tarefa.Id,
            [_origem.AutorId],
            $"seguidas:status-tarefa:{evento.Ocorrencia}",
            $"A tarefa {tarefa.Titulo} agora está {Textos.Status(evento.Para)}",
            _enderecos.TarefaInterna(tarefa.Id),
            cancellationToken);
    }

    private async Task AvisarAsync(
        AlvoColaboracao alvo, Guid alvoId, IEnumerable<Guid?> fora, string chave, string titulo, Uri link, CancellationToken cancellationToken)
    {
        var seguidores = await _seguidores.DoAlvoAsync(alvo, alvoId, cancellationToken);
        if (seguidores.Count == 0)
            return;

        var excluidos = fora.OfType<Guid>().ToHashSet();
        var podemVer = (await _pessoas.ComPermissaoAsync(ComandosSeguimento.PermissaoDeLeitura(alvo), cancellationToken))
            .ToDictionary(p => p.UsuarioId);
        foreach (var id in seguidores.Where(id => !excluidos.Contains(id)))
        {
            if (!podemVer.TryGetValue(id, out var pessoa))
                continue;
            await _aviso.AvisarAsync(
                pessoa,
                CategoriaAviso.Seguidas,
                chave,
                titulo,
                link,
                new MensagemEmail(
                    pessoa.Email,
                    titulo,
                    $"""
                    Olá, {pessoa.Nome}!

                    {titulo}.

                    {link}

                    Você recebe este aviso porque segue este item no Acordu.
                    """,
                    "aviso-seguidas"),
                cancellationToken);
        }
    }

    private async Task<string> NomeAsync(Guid usuarioId, CancellationToken cancellationToken) =>
        (await _pessoas.ObterAsync(usuarioId, cancellationToken))?.Nome ?? "Alguém da equipe";
}
