using Acordu.Application.Anexos;
using Acordu.Application.Common;

namespace Acordu.Application.Tarefas;

/// <summary>Comentário interno de uma tarefa (contrato §8.1a); anexos baixam por <c>/anexos/{id}/download</c>.</summary>
public sealed record ComentarioTarefaDto(
    Guid Id, Referencia Autor, string? Texto, DateTimeOffset RegistradoEm, IReadOnlyList<AnexoDto> Anexos);

/// <summary>Corpo JSON do <c>POST /tarefas/{id}/comentarios</c> (ou multipart com <c>texto</c> e <c>anexos</c>).</summary>
public sealed record ComentarioTarefaRequest(string? Texto);

/// <summary>Leituras dos comentários pelos filtros do contexto (some no contexto de Cliente).</summary>
public interface IConsultasComentariosTarefa
{
    /// <summary>Da tarefa, do mais antigo; nulo se a tarefa não existe (ou não é visível).</summary>
    Task<IReadOnlyList<ComentarioTarefaDto>?> DaTarefaAsync(Guid tarefaId, CancellationToken cancellationToken = default);

    Task<AnexoArmazenado?> AnexoAsync(Guid anexoId, CancellationToken cancellationToken = default);
}
