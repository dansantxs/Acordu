using Acordu.Application.Abstractions;
using Acordu.Application.AcessosPortal;
using Acordu.Domain.Entities;
using Acordu.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence;

/// <summary>Consultas do acesso ao portal, rastreadas e sob os filtros do contexto (tenant da equipe).</summary>
public sealed class AcessosDoPortal : IAcessosDoPortal
{
    private readonly AcorduDbContext _db;
    private readonly ITenantContext _contexto;

    public AcessosDoPortal(AcorduDbContext db, ITenantContext contexto)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(contexto);
        _db = db;
        _contexto = contexto;
    }

    public async Task<IReadOnlyList<ConvitePortal>> ConvitesPendentesAsync(Guid contatoId, CancellationToken cancellationToken = default) =>
        await _db.ConvitesPortal
            .Where(c => c.ContatoId == contatoId && c.AceitoEm == null && c.RevogadoEm == null)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AcessoPortal>> LinksNaoRevogadosAsync(Guid contatoId, CancellationToken cancellationToken = default) =>
        await _db.AcessosPortal
            .Where(a => a.ContatoId == contatoId && a.RevogadoEm == null)
            .ToListAsync(cancellationToken);

    public Task<Vinculo?> VinculoAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        _db.Vinculos.SingleOrDefaultAsync(v => v.UsuarioId == usuarioId, cancellationToken);

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
