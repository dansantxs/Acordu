using Acordu.Application.Anexos;
using Acordu.Application.Common;
using Acordu.Application.Tarefas;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Consultas;

/// <summary>Comentários de tarefa pelos filtros globais (somem no contexto de Cliente).</summary>
public sealed class ConsultasComentariosTarefa : IConsultasComentariosTarefa
{
    private readonly AcorduDbContext _db;

    public ConsultasComentariosTarefa(AcorduDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<IReadOnlyList<ComentarioTarefaDto>?> DaTarefaAsync(Guid tarefaId, CancellationToken cancellationToken = default)
    {
        if (!await _db.Tarefas.AnyAsync(t => t.Id == tarefaId, cancellationToken))
            return null;

        var comentarios = await _db.ComentariosTarefa.AsNoTracking()
            .Where(c => c.TarefaId == tarefaId)
            .OrderBy(c => c.RegistradoEm).ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);
        var pessoas = await Nomes.DeUsuariosAsync(_db, comentarios.Select(c => c.AutorId), cancellationToken);
        return [.. comentarios.Select(c => new ComentarioTarefaDto(
            c.Id,
            new Referencia(c.AutorId, pessoas.GetValueOrDefault(c.AutorId, string.Empty)),
            c.Texto,
            c.RegistradoEm,
            [.. c.Anexos.OrderBy(a => a.NomeArquivo, StringComparer.Ordinal).Select(a => new AnexoDto(a.Id, a.NomeArquivo, a.TipoConteudo, a.TamanhoBytes))]))];
    }

    public async Task<AnexoArmazenado?> AnexoAsync(Guid anexoId, CancellationToken cancellationToken = default) =>
        await _db.ComentariosTarefa.AsNoTracking()
            .SelectMany(c => c.Anexos)
            .Where(a => a.Id == anexoId)
            .Select(a => new AnexoArmazenado(a.ChaveArmazenamento, a.NomeArquivo, a.TipoConteudo))
            .SingleOrDefaultAsync(cancellationToken);
}
