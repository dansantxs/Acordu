using Acordu.Application.Abstractions;
using Acordu.Domain.Entities;
using Acordu.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence;

/// <summary>Vínculos e sessões no tenant do contexto (filtro global de tenant), rastreados para a unidade de trabalho.</summary>
public sealed class Vinculos : IVinculos
{
    private readonly AcorduDbContext _db;
    private readonly ITenantContext _contexto;

    public Vinculos(AcorduDbContext db, ITenantContext contexto)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(contexto);
        _db = db;
        _contexto = contexto;
    }

    public Task<Vinculo?> DoUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        _db.Vinculos.SingleOrDefaultAsync(v => v.UsuarioId == usuarioId, cancellationToken);

    public Task<int> ContarDonosAsync(CancellationToken cancellationToken = default) =>
        (from v in _db.Vinculos
         join p in _db.Papeis on v.PapelId equals p.Id
         where p.Padrao && p.Nome == Papel.NomeDono
         select v.Id).CountAsync(cancellationToken);

    public async Task RevogarSessoesAsync(Guid usuarioId, DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        var tenantId = _contexto.TenantId ?? throw new InvalidOperationException("Revogar sessões exige tenant no contexto.");
        var ativos = await _db.Set<RefreshToken>()
            .Where(r => r.UsuarioId == usuarioId && r.TenantId == tenantId && r.ConsumidoEm == null && r.RevogadoEm == null)
            .ToListAsync(cancellationToken);
        foreach (var token in ativos)
            token.Revogar(agora);
    }

    public Task<bool> EmailJaVinculadoAsync(string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        // Mesma normalização do Identity (UpperInvariantLookupNormalizer).
        var normalizado = email.Trim().ToUpperInvariant();
        return _db.Users
            .Where(u => u.NormalizedEmail == normalizado)
            .AnyAsync(u => _db.Vinculos.Any(v => v.UsuarioId == u.Id), cancellationToken);
    }
}
