using Acordu.Application.Abstractions;
using Acordu.Application.Anexos;
using Acordu.Application.Colaboracao;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Tarefas;

/// <summary>
/// Comentários internos de tarefas (contrato §8.1a, RF-COL-01): registrar com anexos (enviados ao storage antes e
/// descartados se a gravação falhar) e excluir, só pelo autor, apagando os arquivos depois do commit.
/// </summary>
public sealed class ComandosComentarioTarefa
{
    private readonly ITenantContext _contexto;
    private readonly IRepositorio<Tarefa> _tarefas;
    private readonly IRepositorio<ComentarioTarefa> _comentarios;
    private readonly RecebimentoAnexos _anexos;
    private readonly RelogioDoTenant _relogio;
    private readonly IUnitOfWork _unidade;
    private readonly ValidacaoMencoes _mencoes;

    public ComandosComentarioTarefa(
        ITenantContext contexto,
        IRepositorio<Tarefa> tarefas,
        IRepositorio<ComentarioTarefa> comentarios,
        RecebimentoAnexos anexos,
        RelogioDoTenant relogio,
        IUnitOfWork unidade,
        ValidacaoMencoes mencoes)
    {
        ArgumentNullException.ThrowIfNull(mencoes);
        _mencoes = mencoes;
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(tarefas);
        ArgumentNullException.ThrowIfNull(comentarios);
        ArgumentNullException.ThrowIfNull(anexos);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(unidade);
        _contexto = contexto;
        _tarefas = tarefas;
        _comentarios = comentarios;
        _anexos = anexos;
        _relogio = relogio;
        _unidade = unidade;
    }

    public async Task<Guid> ComentarAsync(
        Guid tarefaId, ComentarioTarefaRequest dados, IReadOnlyList<ArquivoRecebido>? arquivos = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        _contexto.Exigir(Permissoes.TarefaEditar);
        var tarefa = await _tarefas.ObterPorIdAsync(tarefaId, cancellationToken)
            ?? throw new NaoEncontradoException("Tarefa não encontrada.");
        await _mencoes.GarantirAsync(dados.Texto, _contexto.Usuario(), Permissoes.TarefaVer, cancellationToken);
        var anexos = await _anexos.EnviarAsync(tarefa.TenantId, tarefa.Id, arquivos, cancellationToken);
        try
        {
            var comentario = ComentarioTarefa.Registrar(
                Guid.CreateVersion7(), tarefa.TenantId, tarefa.Id, _contexto.Usuario(), dados.Texto, anexos,
                RecebimentoAnexos.Politica, _relogio.Agora);
            _comentarios.Adicionar(comentario);
            await _unidade.SalvarAsync(cancellationToken);
            return comentario.Id;
        }
        catch
        {
            await _anexos.DescartarAsync(anexos);
            throw;
        }
    }

    /// <summary>Só o autor exclui (403 para os demais). Os arquivos saem do storage depois do commit, em melhor esforço.</summary>
    public async Task ExcluirAsync(Guid tarefaId, Guid comentarioId, CancellationToken cancellationToken = default)
    {
        _contexto.Exigir(Permissoes.TarefaEditar);
        var comentario = await _comentarios.ObterPorIdAsync(comentarioId, cancellationToken);
        if (comentario is null || comentario.TarefaId != tarefaId)
            throw new NaoEncontradoException("Comentário não encontrado.");
        if (comentario.AutorId != _contexto.Usuario())
            throw new AcessoNegadoException("Só quem escreveu o comentário pode excluí-lo.");

        var arquivos = comentario.Anexos
            .Select(a => new NovoAnexo(a.NomeArquivo, a.TipoConteudo, a.TamanhoBytes, a.ChaveArmazenamento, a.Id))
            .ToList();
        _comentarios.Remover(comentario);
        await _unidade.SalvarAsync(cancellationToken);
        await _anexos.DescartarAsync(arquivos);
    }
}
