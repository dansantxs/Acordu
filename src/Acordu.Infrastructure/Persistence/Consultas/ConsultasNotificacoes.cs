using Acordu.Application.Common;
using Acordu.Application.Notificacoes;
using Acordu.Infrastructure.Persistence.Notificacoes;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Consultas;

/// <summary>Sino do usuário pelos filtros globais (tenant), sempre restrito ao próprio usuário.</summary>
public sealed class ConsultasNotificacoes : IConsultasNotificacoes
{
    private readonly AcorduDbContext _db;

    public ConsultasNotificacoes(AcorduDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<NotificacoesDto> ListarAsync(
        Guid usuarioId, bool apenasNaoLidas, Paginacao paginacao, CancellationToken cancellationToken = default)
    {
        var doUsuario = _db.Set<Notificacao>().AsNoTracking().Where(n => n.UsuarioId == usuarioId);
        var naoLidas = await doUsuario.CountAsync(n => n.LidaEm == null, cancellationToken);
        var consulta = apenasNaoLidas ? doUsuario.Where(n => n.LidaEm == null) : doUsuario;
        var total = apenasNaoLidas ? naoLidas : await consulta.CountAsync(cancellationToken);
        var linhas = await consulta
            .OrderByDescending(n => n.CriadaEm).ThenByDescending(n => n.Id)
            .Skip(paginacao.Pular).Take(paginacao.TamanhoPagina)
            .ToListAsync(cancellationToken);
        return new NotificacoesDto(
            [.. linhas.Select(n => new NotificacaoDto(
                n.Id, Notificacao.DeTexto(n.Categoria), n.Titulo, n.Link is null ? null : new Uri(n.Link), n.CriadaEm, n.LidaEm))],
            paginacao.Pagina,
            paginacao.TamanhoPagina,
            total,
            naoLidas);
    }

    public async Task<bool> MarcarLidaAsync(Guid usuarioId, Guid notificacaoId, DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        var doUsuario = _db.Set<Notificacao>().Where(n => n.UsuarioId == usuarioId && n.Id == notificacaoId);
        if (!await doUsuario.AnyAsync(cancellationToken))
            return false;
        // Idempotente: marcar de novo mantém o primeiro instante.
        await doUsuario.Where(n => n.LidaEm == null).ExecuteUpdateAsync(n => n.SetProperty(x => x.LidaEm, (DateTimeOffset?)agora), cancellationToken);
        return true;
    }

    public Task<int> MarcarTodasAsync(Guid usuarioId, DateTimeOffset agora, CancellationToken cancellationToken = default) =>
        _db.Set<Notificacao>()
            .Where(n => n.UsuarioId == usuarioId && n.LidaEm == null)
            .ExecuteUpdateAsync(n => n.SetProperty(x => x.LidaEm, (DateTimeOffset?)agora), cancellationToken);
}
