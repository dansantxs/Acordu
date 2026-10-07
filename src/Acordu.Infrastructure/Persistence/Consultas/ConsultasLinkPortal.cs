using Acordu.Application.AcessosPortal;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Consultas;

/// <summary>Links válidos de uma solicitação, com o nome do contato (sem o token, que só existe na emissão).</summary>
public sealed class ConsultasLinkPortal : IConsultasLinkPortal
{
    private readonly AcorduDbContext _db;

    public ConsultasLinkPortal(AcorduDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<IReadOnlyList<LinkPortalDto>?> ListarAsync(
        Guid solicitacaoId, DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        var clienteId = await _db.Solicitacoes
            .Where(s => s.Id == solicitacaoId)
            .Select(s => (Guid?)s.ClienteId)
            .SingleOrDefaultAsync(cancellationToken);
        if (clienteId is null)
            return null;

        var acessos = await _db.AcessosPortal.AsNoTracking()
            .Where(a => a.SolicitacaoId == solicitacaoId && a.RevogadoEm == null && a.ExpiraEm > agora)
            .OrderBy(a => a.EmitidoEm)
            .ToListAsync(cancellationToken);
        var contatos = await _db.Clientes.AsNoTracking()
            .Where(c => c.Id == clienteId)
            .SelectMany(c => c.Contatos)
            .ToDictionaryAsync(c => c.Id, c => c.Nome, cancellationToken);
        return [.. acessos.Select(a => new LinkPortalDto(
            a.Id, a.ContatoId, contatos.GetValueOrDefault(a.ContatoId, string.Empty), a.EmitidoEm, a.ExpiraEm))];
    }
}
