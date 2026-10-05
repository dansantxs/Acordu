using Acordu.Application.Abstractions;
using Acordu.Infrastructure.Persistence.Eventos;
using Acordu.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence;

/// <summary>
/// Transação do caso de uso: despacha os eventos para os manipuladores internos, grava a outbox com todos eles,
/// salva e faz o commit (inclusive de uma transação já aberta pelo <c>GeradorProtocolo</c>). Qualquer falha desfaz tudo.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AcorduDbContext _db;
    private readonly DespachanteEventos _despachante;
    private readonly TimeProvider _relogio;

    public UnitOfWork(AcorduDbContext db, DespachanteEventos despachante, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(despachante);
        ArgumentNullException.ThrowIfNull(relogio);
        _db = db;
        _despachante = despachante;
        _relogio = relogio;
    }

    public async Task SalvarAsync(CancellationToken cancellationToken = default)
    {
        var transacao = _db.Database.CurrentTransaction ?? await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var eventos = await _despachante.DespacharAsync(_db, cancellationToken);
            var agora = _relogio.GetUtcNow();
            foreach (var (evento, tenantId) in eventos)
            {
                _db.Add(new MensagemOutbox(
                    Guid.CreateVersion7(), tenantId, SerializadorEventos.Tipo(evento), SerializadorEventos.Serializar(evento), agora));
            }

            await _db.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
        }
        catch
        {
            await transacao.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            await transacao.DisposeAsync();
        }
    }
}
