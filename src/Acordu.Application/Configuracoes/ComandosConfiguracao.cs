using System.Globalization;
using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Application.Contratos;
using Acordu.Application.EntradaEmail;
using Acordu.Domain.Acesso;
using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Domain.Exceptions;
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
    private readonly LogotipoPortal _logotipo;
    private readonly OpcoesEmailEntrada _entrada;

    public ComandosConfiguracao(
        ITenantContext contexto,
        ICalendarios calendarios,
        ITenantAtual tenant,
        IUnitOfWork unidade,
        LogotipoPortal logotipo,
        OpcoesEmailEntrada entrada)
    {
        ArgumentNullException.ThrowIfNull(logotipo);
        ArgumentNullException.ThrowIfNull(entrada);
        _logotipo = logotipo;
        _entrada = entrada;
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

    public ConfiguracaoPortalDto Portal(Tenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        return new ConfiguracaoPortalDto(tenant.Nome, tenant.NomePortal, tenant.DiasFechamentoAutomatico, _logotipo.Url(tenant.LogoChave));
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

    /// <summary>
    /// A configuração da empresa, com o endereço de entrada e o recebimento por e-mail (F15-95): o endereço vem nulo na
    /// demonstração (F15-96) e sem o domínio de entrada configurado (F15-94).
    /// </summary>
    public ConfiguracaoEmpresaDto Empresa(Tenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        return new ConfiguracaoEmpresaDto(
            tenant.Nome,
            tenant.ResponderPara,
            [.. tenant.SlaSugerido.Prazos
                .OrderByDescending(p => p.Prioridade)
                .Select(p => new PrazoSlaDto(p.Prioridade, p.PrimeiraResposta.Minutos, p.Resolucao.Minutos))],
            tenant.SlaPadrao is not null,
            _entrada.EnderecoDe(tenant),
            tenant.RecebimentoEmail);
    }

    /// <summary>Leitura liberada à equipe: o cadastro de contrato usa o SLA sugerido; o endereço de entrada também (F15-95).</summary>
    public async Task<ConfiguracaoEmpresaDto> ObterEmpresaAsync(CancellationToken cancellationToken = default) =>
        Empresa(await _tenant.ObterAsync(cancellationToken));

    public async Task<ConfiguracaoEmpresaDto> AtualizarEmpresaAsync(
        ConfiguracaoEmpresaRequest dados, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        _contexto.Exigir(Permissoes.ConfiguracaoGerenciar);
        if (string.IsNullOrWhiteSpace(dados.Nome))
            throw ErroDeCampo.Criar("nome", "Informe o nome da empresa.");
        if (dados.Nome.Trim().Length > LimitesTexto.Nome)
            throw ErroDeCampo.Criar("nome", $"O nome vai até {LimitesTexto.Nome} caracteres.");
        var sla = SlaPadrao(dados.SlaPadrao);

        var tenant = await _tenant.ObterAsync(cancellationToken);
        try
        {
            tenant.ConfigurarEmpresa(dados.Nome, dados.ResponderPara, sla);
        }
        catch (ArgumentException e) when (e.ParamName == "responderPara")
        {
            throw ErroDeCampo.Criar("responderPara", "E-mail inválido.");
        }

        await _unidade.SalvarAsync(cancellationToken);
        return Empresa(tenant);
    }

    /// <summary>As quatro prioridades, com o erro no campo <c>slaPadrao</c> (no contrato, a mesma regra vira 422).</summary>
    private static PoliticaSla SlaPadrao(IReadOnlyList<PrazoSlaDto>? sla)
    {
        if (sla is null || sla.Count == 0)
            throw ErroDeCampo.Criar("slaPadrao", "Informe os prazos das quatro prioridades.");
        if (sla.Any(p => p.PrimeiraRespostaMinutos <= 0 || p.ResolucaoMinutos <= 0))
            throw ErroDeCampo.Criar("slaPadrao", "Os prazos precisam ser maiores que zero.");
        try
        {
            return new PoliticaSla(sla.Select(p => new PrazoSla(
                p.Prioridade, Duracao.FromMinutos(p.PrimeiraRespostaMinutos), Duracao.FromMinutos(p.ResolucaoMinutos))));
        }
        catch (Exception e) when (e is SlaIncompletoException or ArgumentOutOfRangeException)
        {
            throw ErroDeCampo.Criar("slaPadrao", e.Message);
        }
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
