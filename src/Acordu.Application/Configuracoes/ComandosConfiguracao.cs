using System.Globalization;
using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Configuracoes;

/// <summary>
/// Calendário e configurações do portal (contrato §6). Mudar o calendário não recalcula prazos de solicitações já
/// abertas: eles foram fixados com o calendário da época; só o que vier depois usa o novo.
/// </summary>
public sealed class ComandosConfiguracao
{
    private readonly ITenantContext _contexto;
    private readonly ICalendarios _calendarios;
    private readonly ITenantAtual _tenant;
    private readonly IUnitOfWork _unidade;

    public ComandosConfiguracao(ITenantContext contexto, ICalendarios calendarios, ITenantAtual tenant, IUnitOfWork unidade)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(calendarios);
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(unidade);
        _contexto = contexto;
        _calendarios = calendarios;
        _tenant = tenant;
        _unidade = unidade;
    }

    public static CalendarioDto Calendario(CalendarioUtil calendario)
    {
        ArgumentNullException.ThrowIfNull(calendario);
        return new CalendarioDto(
            calendario.FusoHorario,
            [.. calendario.Expediente.OrderBy(j => j.Dia).Select(j => new JanelaDto((DiaDaSemana)j.Dia, Hora(j.Inicio), Hora(j.Fim)))],
            [.. calendario.Feriados]);
    }

    public static ConfiguracaoPortalDto Portal(Tenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        return new ConfiguracaoPortalDto(tenant.Nome, tenant.NomePortal, tenant.DiasFechamentoAutomatico);
    }

    public async Task<CalendarioDto> AtualizarCalendarioAsync(CalendarioRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        _contexto.Exigir(Permissoes.ConfiguracaoGerenciar);
        var fuso = dados.FusoHorario?.Trim();
        if (string.IsNullOrEmpty(fuso) || !TimeZoneInfo.TryFindSystemTimeZoneById(fuso, out _))
            throw ErroDeCampo.Criar("fusoHorario", "Fuso horário desconhecido (use um identificador IANA, como America/Sao_Paulo).");
        var expediente = Expediente(dados.Expediente);

        var calendario = await _calendarios.ParaEdicaoAsync(cancellationToken);
        calendario.AlterarFuso(fuso);
        calendario.DefinirExpediente(expediente);
        calendario.DefinirFeriados(dados.Feriados ?? []);
        await _unidade.SalvarAsync(cancellationToken);
        return Calendario(calendario);
    }

    public async Task<ConfiguracaoPortalDto> ObterPortalAsync(CancellationToken cancellationToken = default)
    {
        _contexto.Exigir(Permissoes.ConfiguracaoGerenciar);
        return Portal(await _tenant.ObterAsync(cancellationToken));
    }

    public async Task<ConfiguracaoPortalDto> AtualizarPortalAsync(ConfiguracaoPortalRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        _contexto.Exigir(Permissoes.ConfiguracaoGerenciar);
        if (dados.DiasFechamentoAutomatico is < Tenant.DiasFechamentoMinimo or > Tenant.DiasFechamentoMaximo)
        {
            throw ErroDeCampo.Criar(
                "diasFechamentoAutomatico",
                $"O fechamento automático vai de {Tenant.DiasFechamentoMinimo} a {Tenant.DiasFechamentoMaximo} dias.");
        }

        var tenant = await _tenant.ObterAsync(cancellationToken);
        tenant.ConfigurarPortal(dados.NomeExibido, dados.DiasFechamentoAutomatico);
        await _unidade.SalvarAsync(cancellationToken);
        return Portal(tenant);
    }

    /// <summary>Valida o expediente com erro no campo (o domínio também valida, com mensagens sem o campo do JSON).</summary>
    private static List<JanelaDiaria> Expediente(IReadOnlyList<JanelaDto>? janelas)
    {
        if (janelas is null || janelas.Count == 0)
            throw ErroDeCampo.Criar("expediente", "O calendário precisa de ao menos um dia útil na semana.");
        if (janelas.GroupBy(j => j.DiaSemana).FirstOrDefault(g => g.Count() > 1) is { } repetido)
            throw ErroDeCampo.Criar("expediente", $"Há mais de uma janela para {repetido.Key}.");

        return [.. janelas.Select(j =>
        {
            if (!Enum.IsDefined(j.DiaSemana))
                throw ErroDeCampo.Criar("expediente", "Dia da semana inválido.");
            var inicio = LerHora(j.Inicio);
            var fim = LerHora(j.Fim);
            if (fim <= inicio)
                throw ErroDeCampo.Criar("expediente", $"No dia {j.DiaSemana}, o fim deve ser depois do início.");
            return new JanelaDiaria((DayOfWeek)j.DiaSemana, inicio, fim);
        })];
    }

    private static TimeOnly LerHora(string? valor) =>
        TimeOnly.TryParseExact(valor, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var hora)
            ? hora
            : throw ErroDeCampo.Criar("expediente", "Horários no formato HH:mm.");

    private static string Hora(TimeOnly hora) => hora.ToString("HH:mm", CultureInfo.InvariantCulture);
}
