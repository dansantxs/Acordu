using Acordu.Application.Abstractions;
using Acordu.Infrastructure.Persistence.Eventos;
using Acordu.Infrastructure.Persistence.Outbox;
using Acordu.Infrastructure.TempoReal;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence;

/// <summary>
/// Transação do caso de uso: despacha os eventos para os manipuladores internos, grava a outbox com todos eles,
/// salva e faz o commit (inclusive de uma transação já aberta pelo <c>GeradorProtocolo</c>). Qualquer falha desfaz tudo.
/// Depois do commit, publica os sinais de mudança para o tempo real (ADR 0009).
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AcorduDbContext _db;
    private readonly DespachanteEventos _despachante;
    private readonly TimeProvider _relogio;
    private readonly MensagensDaRequisicao _mensagens;
    private readonly ITenantContext _contexto;
    private readonly DifusorSinais _difusor;

    public UnitOfWork(
        AcorduDbContext db,
        DespachanteEventos despachante,
        TimeProvider relogio,
        MensagensDaRequisicao mensagens,
        ITenantContext contexto,
        DifusorSinais difusor)
    {
        ArgumentNullException.ThrowIfNull(difusor);
        _difusor = difusor;
        ArgumentNullException.ThrowIfNull(contexto);
        _contexto = contexto;
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(despachante);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(mensagens);
        _db = db;
        _despachante = despachante;
        _relogio = relogio;
        _mensagens = mensagens;
    }

    public async Task SalvarAsync(CancellationToken cancellationToken = default)
    {
        var transacao = _db.Database.CurrentTransaction ?? await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var eventos = await _despachante.DespacharAsync(_db, cancellationToken);
            var agora = _relogio.GetUtcNow();
            var gravadas = new List<Guid>();
            foreach (var (evento, tenantId) in eventos)
            {
                var mensagem = new MensagemOutbox(
                    Guid.CreateVersion7(), tenantId, SerializadorEventos.Tipo(evento), SerializadorEventos.Serializar(evento), agora,
                    _contexto.UsuarioId);
                _db.Add(mensagem);
                gravadas.Add(mensagem.Id);
            }

            var sinais = ColetorSinais.Coletar(_db.ChangeTracker);
            await _db.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
            _mensagens.Adicionar(gravadas);
            foreach (var porTenant in sinais.GroupBy(s => s.TenantId))
                _difusor.Publicar(porTenant.Key, porTenant.Select(s => s.Sinal));
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
