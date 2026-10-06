using Acordu.Application.Abstractions;
using Acordu.Application.Portal;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence;

/// <summary>
/// Contato do usuário no cliente do contexto (ativo, de cliente ativo) e o escopo do vínculo, lidos uma vez por
/// requisição. Revogar remove o vínculo e desvincula o contato: a próxima requisição já volta nula.
/// </summary>
public sealed class PortalAtual : IPortalAtual
{
    private readonly AcorduDbContext _db;
    private readonly ITenantContext _contexto;
    private Task<ContatoDoPortal?>? _contato;

    public PortalAtual(AcorduDbContext db, ITenantContext contexto)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(contexto);
        _db = db;
        _contexto = contexto;
    }

    public Task<ContatoDoPortal?> ObterAsync(CancellationToken cancellationToken = default) =>
        _contato ??= CarregarAsync(cancellationToken);

    private async Task<ContatoDoPortal?> CarregarAsync(CancellationToken cancellationToken)
    {
        if (!_contexto.EhCliente || _contexto.ClienteId is not { } clienteId || _contexto.UsuarioId is not { } usuarioId)
            return null;

        var escopo = await _db.Vinculos.AsNoTracking()
            .Where(v => v.UsuarioId == usuarioId && v.ClienteId == clienteId)
            .Select(v => v.EscopoPortal)
            .SingleOrDefaultAsync(cancellationToken);
        if (escopo is null)
            return null;

        var contatoId = await _db.Clientes.AsNoTracking()
            .Where(c => c.Id == clienteId && c.Ativo)
            .SelectMany(c => c.Contatos)
            .Where(c => c.UsuarioId == usuarioId && c.Ativo)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return contatoId is { } id ? new ContatoDoPortal(id, clienteId, escopo.Value) : null;
    }
}
