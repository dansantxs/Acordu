using Acordu.Application.EntradaEmail;
using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.EntradaEmail;

/// <summary>A conversa por e-mail de uma solicitação, rastreada e sob os filtros do contexto (tenant e cliente).</summary>
public sealed class ConversasPorEmail : IConversasPorEmail
{
    private readonly AcorduDbContext _db;

    public ConversasPorEmail(AcorduDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public Task<ConversaPorEmail?> DaSolicitacaoAsync(Guid solicitacaoId, CancellationToken cancellationToken = default) =>
        _db.ConversasPorEmail.SingleOrDefaultAsync(c => c.SolicitacaoId == solicitacaoId, cancellationToken);
}
