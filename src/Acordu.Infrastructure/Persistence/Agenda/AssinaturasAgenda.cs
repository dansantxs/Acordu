using Acordu.Application.Abstractions;
using Acordu.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Agenda;

/// <summary>
/// Links da agenda assinável pelo EF (gravados no <c>SalvarAsync</c> do caso de uso). A resolução do token ignora os
/// filtros: o feed chega sem sessão e é o token que diz o tenant e a pessoa.
/// </summary>
public sealed class AssinaturasAgenda : IAssinaturasAgenda
{
    private readonly AcorduDbContext _db;
    private readonly ITenantContext _contexto;
    private readonly TimeProvider _relogio;

    public AssinaturasAgenda(AcorduDbContext db, ITenantContext contexto, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(relogio);
        _db = db;
        _contexto = contexto;
        _relogio = relogio;
    }

    public async Task<string> EmitirAsync(Guid usuarioId, bool discreta, CancellationToken cancellationToken = default)
    {
        var tenant = _contexto.TenantId ?? throw new InvalidOperationException("Link da agenda exige tenant no contexto.");
        var token = TokenSeguro.Gerar();
        var hash = TokenSeguro.Hash(token);
        var agora = _relogio.GetUtcNow();
        if (await Da(usuarioId).SingleOrDefaultAsync(cancellationToken) is { } atual)
            atual.Renovar(hash, discreta, agora);
        else
            _db.Add(new AssinaturaAgendaRegistro(tenant, usuarioId, hash, discreta, agora));
        return token;
    }

    public async Task<AssinaturaAgenda?> DaPessoaAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        await Da(usuarioId).AsNoTracking()
            .Select(a => new AssinaturaAgenda(a.Discreta, a.CriadaEm))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> AlterarModoAsync(Guid usuarioId, bool discreta, CancellationToken cancellationToken = default)
    {
        if (await Da(usuarioId).SingleOrDefaultAsync(cancellationToken) is not { } atual)
            return false;
        atual.DefinirModo(discreta);
        return true;
    }

    public async Task<bool> RevogarAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        if (await Da(usuarioId).SingleOrDefaultAsync(cancellationToken) is not { } atual)
            return false;
        _db.Remove(atual);
        return true;
    }

    public async Task<AssinaturaResolvida?> ResolverAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(token))
            return null;
        var hash = TokenSeguro.Hash(token);
        return await _db.Set<AssinaturaAgendaRegistro>().IgnoreQueryFilters().AsNoTracking()
            .Where(a => a.TokenHash == hash)
            .Select(a => new AssinaturaResolvida(a.TenantId, a.UsuarioId, a.Discreta))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private IQueryable<AssinaturaAgendaRegistro> Da(Guid usuarioId) =>
        _db.Set<AssinaturaAgendaRegistro>().Where(a => a.UsuarioId == usuarioId);
}
