using Acordu.Application.Abstractions;
using Acordu.Application.Notificacoes;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence.Notificacoes;

/// <summary>Grava a notificação na hora (pós-commit ou job), idempotente por (tenant, usuário, chave).</summary>
public sealed class RegistroNotificacoes : INotificacoes
{
    private readonly AcorduDbContext _db;
    private readonly ITenantContext _contexto;
    private readonly TimeProvider _relogio;

    public RegistroNotificacoes(AcorduDbContext db, ITenantContext contexto, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(relogio);
        _db = db;
        _contexto = contexto;
        _relogio = relogio;
    }

    public async Task RegistrarAsync(
        Guid usuarioId, CategoriaAviso categoria, string chave, string titulo, Uri? link, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chave);
        ArgumentException.ThrowIfNullOrWhiteSpace(titulo);
        var tenant = _contexto.TenantId ?? throw new InvalidOperationException("Notificação exige tenant no contexto.");
        var agora = _relogio.GetUtcNow();
        var id = Guid.CreateVersion7(agora);
        var texto = Notificacao.Texto(categoria);
        var chaveCurta = Cortar(chave, Notificacao.TamanhoChave);
        var tituloCurto = Cortar(titulo, Notificacao.TamanhoTitulo);
        var endereco = link?.AbsoluteUri is { Length: <= Notificacao.TamanhoLink } absoluto ? absoluto : null;
        await _db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO notificacoes (id, tenant_id, usuario_id, categoria, chave, titulo, link, criada_em)
            VALUES ({id}, {tenant}, {usuarioId}, {texto}, {chaveCurta}, {tituloCurto}, {endereco}, {agora})
            ON CONFLICT (tenant_id, usuario_id, chave) DO NOTHING
            """,
            cancellationToken);
    }

    private static string Cortar(string texto, int tamanho) => texto.Length <= tamanho ? texto : texto[..tamanho];
}

/// <summary>Preferências de e-mail por pessoa no tenant do contexto (linha só quando algo foi desligado).</summary>
public sealed class PreferenciasNotificacao : IPreferenciasNotificacao
{
    private readonly AcorduDbContext _db;
    private readonly ITenantContext _contexto;

    public PreferenciasNotificacao(AcorduDbContext db, ITenantContext contexto)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(contexto);
        _db = db;
        _contexto = contexto;
    }

    public async Task<IReadOnlySet<CategoriaAviso>> EmailDesligadoAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var linha = await _db.Set<PreferenciaNotificacao>().AsNoTracking()
            .Where(p => p.UsuarioId == usuarioId)
            .Select(p => p.EmailDesligado)
            .SingleOrDefaultAsync(cancellationToken);
        return linha is null ? new HashSet<CategoriaAviso>() : linha.Select(Notificacao.DeTexto).ToHashSet();
    }

    public async Task SalvarAsync(Guid usuarioId, IReadOnlySet<CategoriaAviso> emailDesligado, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(emailDesligado);
        var tenant = _contexto.TenantId ?? throw new InvalidOperationException("Preferências exigem tenant no contexto.");
        var linha = await _db.Set<PreferenciaNotificacao>().SingleOrDefaultAsync(p => p.UsuarioId == usuarioId, cancellationToken);
        if (linha is null)
        {
            linha = new PreferenciaNotificacao(tenant, usuarioId);
            _db.Add(linha);
        }

        linha.Definir(emailDesligado.Select(Notificacao.Texto));
        await _db.SaveChangesAsync(cancellationToken);
    }
}
