using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;

namespace Acordu.Application.Dashboard;

/// <summary>
/// Monta o dashboard com o período (padrão: mês corrente no fuso do tenant; até 366 dias) e os blocos que a pessoa pode
/// ver. Período vale para cumprimento de SLA e horas; fila, SLA em aberto, contratos e tarefas são do momento.
/// </summary>
public sealed class LeituraDashboard
{
    public const int MaximoDias = 366;
    public const int ClientesNoTopo = 10;
    public const int ProximasRecorrentes = 5;

    private readonly ITenantContext _contexto;
    private readonly IConsultasDashboard _consultas;
    private readonly ICalendarios _calendarios;
    private readonly RelogioDoTenant _relogio;

    public LeituraDashboard(ITenantContext contexto, IConsultasDashboard consultas, ICalendarios calendarios, RelogioDoTenant relogio)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(consultas);
        ArgumentNullException.ThrowIfNull(calendarios);
        ArgumentNullException.ThrowIfNull(relogio);
        _contexto = contexto;
        _consultas = consultas;
        _calendarios = calendarios;
        _relogio = relogio;
    }

    public async Task<DashboardDto> ObterAsync(DateOnly? de, DateOnly? ate, CancellationToken cancellationToken = default)
    {
        _contexto.Exigir(Permissoes.DashboardVer);
        var hoje = await _relogio.HojeAsync(cancellationToken);
        var inicio = de ?? new DateOnly(hoje.Year, hoje.Month, 1);
        var fim = ate ?? new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(1).AddDays(-1);
        if (fim < inicio)
            throw ErroDeCampo.Criar("ate", "O fim do período não pode ser anterior ao início.");
        if (fim.DayNumber - inicio.DayNumber + 1 > MaximoDias)
            throw ErroDeCampo.Criar("ate", $"O período vai até {MaximoDias} dias.");

        var fila = Pode(Permissoes.SolicitacaoVer) ? await _consultas.FilaAsync(cancellationToken) : null;
        SlaDashboardDto? sla = null;
        if (Pode(Permissoes.SolicitacaoVer))
        {
            var fuso = (await _calendarios.DoTenantAsync(cancellationToken)).Fuso;
            sla = await _consultas.SlaAsync(_relogio.Agora, Instante(inicio, fuso), Instante(fim.AddDays(1), fuso), cancellationToken);
        }

        var horas = Pode(Permissoes.ApontamentoVer) ? await _consultas.HorasAsync(inicio, fim, ClientesNoTopo, cancellationToken) : null;
        var contratos = Pode(Permissoes.ContratoVer) ? await _consultas.ContratosAsync(hoje, cancellationToken) : null;
        TarefasDashboardDto? tarefas = null;
        if (Pode(Permissoes.TarefaVer))
        {
            var recorrentes = Pode(Permissoes.RecorrenciaVer)
                ? await _consultas.ProximasRecorrentesAsync(ProximasRecorrentes, cancellationToken)
                : null;
            tarefas = new TarefasDashboardDto(await _consultas.TarefasVencidasAsync(hoje, cancellationToken), recorrentes);
        }

        return new DashboardDto(inicio, fim, fila, sla, horas, contratos, tarefas);
    }

    private bool Pode(string permissao) => _contexto.Permissoes.Contains(permissao);

    private static DateTimeOffset Instante(DateOnly dia, TimeZoneInfo fuso)
    {
        var local = dia.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, fuso.GetUtcOffset(local)).ToUniversalTime();
    }
}
