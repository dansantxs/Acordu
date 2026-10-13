using Acordu.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence;

/// <summary>
/// Marcador de aviso enviado: linha em <c>marcadores_aviso</c>, chave primária (tenant, referência, chave). Inserção com
/// <c>ON CONFLICT DO NOTHING</c>: só quem inseriu envia, mesmo com dois ticks concorrentes.
/// </summary>
public sealed class MarcadoresDeAviso : IMarcadoresDeAviso
{
    private readonly AcorduDbContext _db;
    private readonly ITenantContext _contexto;
    private readonly TimeProvider _relogio;

    public MarcadoresDeAviso(AcorduDbContext db, ITenantContext contexto, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(relogio);
        _db = db;
        _contexto = contexto;
        _relogio = relogio;
    }

    public async Task<bool> MarcarAsync(Guid referencia, string chave, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chave);
        var tenant = Tenant();
        var agora = _relogio.GetUtcNow();
        var inseridas = await _db.Database.ExecuteSqlAsync(
            $"INSERT INTO marcadores_aviso (tenant_id, referencia, chave, criado_em) VALUES ({tenant}, {referencia}, {chave}, {agora}) ON CONFLICT DO NOTHING",
            cancellationToken);
        return inseridas == 1;
    }

    public async Task DesmarcarAsync(Guid referencia, string chave, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chave);
        var tenant = Tenant();
        await _db.Database.ExecuteSqlAsync(
            $"DELETE FROM marcadores_aviso WHERE tenant_id = {tenant} AND referencia = {referencia} AND chave = {chave}",
            cancellationToken);
    }

    private Guid Tenant() =>
        _contexto.TenantId ?? throw new InvalidOperationException("Marcador de aviso exige tenant no contexto.");
}

/// <summary>Só para o mapeamento (migration); lida e escrita por SQL em <see cref="MarcadoresDeAviso"/>.</summary>
public sealed class MarcadorAviso
{
    private MarcadorAviso()
    {
        Chave = null!;
    }

    public Guid TenantId { get; private set; }

    public Guid Referencia { get; private set; }

    public string Chave { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }
}
