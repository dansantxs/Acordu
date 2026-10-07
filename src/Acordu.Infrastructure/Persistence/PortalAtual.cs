using Acordu.Application.Abstractions;
using Acordu.Application.Portal;
using Acordu.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence;

/// <summary>
/// Contato do usuário no cliente do contexto (ativo, de cliente ativo) e o escopo do vínculo, lidos uma vez por
/// requisição. Revogar remove o vínculo e desvincula o contato: a próxima requisição já volta nula. Na sessão de link,
/// o acesso precisa continuar válido (não revogado nem expirado) e o contato dele, ativo.
/// </summary>
public sealed class PortalAtual : IPortalAtual
{
    private readonly AcorduDbContext _db;
    private readonly ITenantContext _contexto;
    private readonly TimeProvider _relogio;
    private Task<ContatoDoPortal?>? _contato;

    public PortalAtual(AcorduDbContext db, ITenantContext contexto, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(relogio);
        _db = db;
        _contexto = contexto;
        _relogio = relogio;
    }

    public Task<ContatoDoPortal?> ObterAsync(CancellationToken cancellationToken = default) =>
        _contato ??= CarregarAsync(cancellationToken);

    private async Task<ContatoDoPortal?> CarregarAsync(CancellationToken cancellationToken)
    {
        if (!_contexto.EhCliente || _contexto.ClienteId is not { } clienteId)
            return null;
        if (_contexto.AcessoPortalId is { } acessoId)
            return await DoLinkAsync(clienteId, acessoId, cancellationToken);
        if (_contexto.UsuarioId is not { } usuarioId)
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

    private async Task<ContatoDoPortal?> DoLinkAsync(Guid clienteId, Guid acessoId, CancellationToken cancellationToken)
    {
        var acesso = await _db.AcessosPortal.AsNoTracking().SingleOrDefaultAsync(a => a.Id == acessoId, cancellationToken);
        if (acesso is null || !acesso.EstaValido(_relogio.GetUtcNow()) || acesso.SolicitacaoId != _contexto.SolicitacaoPermitida)
            return null;

        var contatoAtivo = await _db.Clientes.AsNoTracking()
            .Where(c => c.Id == clienteId && c.Ativo)
            .SelectMany(c => c.Contatos)
            .AnyAsync(c => c.Id == acesso.ContatoId && c.Ativo, cancellationToken);
        return contatoAtivo
            ? new ContatoDoPortal(acesso.ContatoId, clienteId, EscopoPortal.Proprias, acesso.SolicitacaoId)
            : null;
    }
}
