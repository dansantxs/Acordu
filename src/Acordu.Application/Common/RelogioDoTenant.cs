using Acordu.Application.Abstractions;

namespace Acordu.Application.Common;

/// <summary>Datas civis no fuso do tenant: "hoje", mês corrente. O domínio não lê relógio; a Application passa.</summary>
public sealed class RelogioDoTenant
{
    private readonly ICalendarios _calendarios;
    private readonly TimeProvider _relogio;

    public RelogioDoTenant(ICalendarios calendarios, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(calendarios);
        ArgumentNullException.ThrowIfNull(relogio);
        _calendarios = calendarios;
        _relogio = relogio;
    }

    public DateTimeOffset Agora => _relogio.GetUtcNow();

    public async Task<DateOnly> HojeAsync(CancellationToken cancellationToken = default)
    {
        var calendario = await _calendarios.DoTenantAsync(cancellationToken);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_relogio.GetUtcNow(), calendario.Fuso).DateTime);
    }
}
