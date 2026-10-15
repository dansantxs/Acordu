using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;

namespace Acordu.Application.Colaboracao;

/// <summary>Seguir e deixar de seguir uma solicitação ou tarefa, e ver quem segue (contrato §7 e §8.1, RF-COL-03).</summary>
public sealed class ComandosSeguimento
{
    private readonly ITenantContext _contexto;
    private readonly ISeguidores _seguidores;
    private readonly IRepositorio<Solicitacao> _solicitacoes;
    private readonly IRepositorio<Tarefa> _tarefas;

    public ComandosSeguimento(
        ITenantContext contexto, ISeguidores seguidores, IRepositorio<Solicitacao> solicitacoes, IRepositorio<Tarefa> tarefas)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(seguidores);
        ArgumentNullException.ThrowIfNull(solicitacoes);
        ArgumentNullException.ThrowIfNull(tarefas);
        _contexto = contexto;
        _seguidores = seguidores;
        _solicitacoes = solicitacoes;
        _tarefas = tarefas;
    }

    public static string PermissaoDeLeitura(AlvoColaboracao alvo) =>
        alvo == AlvoColaboracao.Solicitacao ? Permissoes.SolicitacaoVer : Permissoes.TarefaVer;

    public async Task<IReadOnlyList<Referencia>> ListarAsync(AlvoColaboracao alvo, Guid alvoId, CancellationToken cancellationToken = default)
    {
        await GarantirAlvoAsync(alvo, alvoId, cancellationToken);
        return await _seguidores.ListarAsync(alvo, alvoId, cancellationToken);
    }

    public async Task SeguirAsync(AlvoColaboracao alvo, Guid alvoId, CancellationToken cancellationToken = default)
    {
        await GarantirAlvoAsync(alvo, alvoId, cancellationToken);
        await _seguidores.SeguirAsync(alvo, alvoId, _contexto.Usuario(), cancellationToken);
    }

    public async Task DeixarAsync(AlvoColaboracao alvo, Guid alvoId, CancellationToken cancellationToken = default)
    {
        await GarantirAlvoAsync(alvo, alvoId, cancellationToken);
        await _seguidores.DeixarAsync(alvo, alvoId, _contexto.Usuario(), cancellationToken);
    }

    private async Task GarantirAlvoAsync(AlvoColaboracao alvo, Guid alvoId, CancellationToken cancellationToken)
    {
        _contexto.Exigir(PermissaoDeLeitura(alvo));
        var existe = alvo == AlvoColaboracao.Solicitacao
            ? await _solicitacoes.ObterPorIdAsync(alvoId, cancellationToken) is not null
            : await _tarefas.ObterPorIdAsync(alvoId, cancellationToken) is not null;
        if (!existe)
            throw new NaoEncontradoException(alvo == AlvoColaboracao.Solicitacao ? "Solicitação não encontrada." : "Tarefa não encontrada.");
    }
}
