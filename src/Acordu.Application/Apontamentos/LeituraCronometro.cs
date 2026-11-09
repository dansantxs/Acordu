using Acordu.Application.Abstractions;
using Acordu.Application.Common;

namespace Acordu.Application.Apontamentos;

/// <summary>Cronômetro da pessoa com a avaliação de esquecido (F12-17), no calendário e no relógio do tenant.</summary>
public sealed class LeituraCronometro
{
    private readonly IConsultasApontamento _consultas;
    private readonly ICalendarios _calendarios;
    private readonly RelogioDoTenant _relogio;

    public LeituraCronometro(IConsultasApontamento consultas, ICalendarios calendarios, RelogioDoTenant relogio)
    {
        ArgumentNullException.ThrowIfNull(consultas);
        ArgumentNullException.ThrowIfNull(calendarios);
        ArgumentNullException.ThrowIfNull(relogio);
        _consultas = consultas;
        _calendarios = calendarios;
        _relogio = relogio;
    }

    public async Task<CronometroDto?> ObterAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        if (await _consultas.CronometroAsync(usuarioId, cancellationToken) is not { } cronometro)
            return null;
        var calendario = await _calendarios.DoTenantAsync(cancellationToken);
        var (esquecido, sugestao) = CronometroEsquecido.Avaliar(cronometro.IniciadoEm, _relogio.Agora, calendario);
        return cronometro with { Esquecido = esquecido, SugestaoMinutos = sugestao };
    }
}
