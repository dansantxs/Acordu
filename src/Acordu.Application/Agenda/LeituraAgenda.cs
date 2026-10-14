using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;
using Acordu.Domain.Enums;
using Acordu.Domain.Services;

namespace Acordu.Application.Agenda;

/// <summary>
/// Monta a agenda (contrato §11b, read side §6a). Período padrão: da segunda-feira desta semana até o domingo da
/// seguinte (duas semanas); no máximo <see cref="MaximoDias"/> dias. Se o período inclui hoje, os itens em aberto com data
/// anterior (solicitações violadas, tarefas e projetos atrasados) vêm junto, com a data real, para não sumirem da tela.
/// </summary>
public sealed class LeituraAgenda
{
    public const int MaximoDias = 62;

    /// <summary>Permissões que abrem a agenda (basta uma); cada tipo de item exige a sua.</summary>
    public static readonly string[] PermissoesDeLeitura =
    [
        Permissoes.SolicitacaoVer, Permissoes.TarefaVer, Permissoes.ProjetoVer, Permissoes.RecorrenciaVer, Permissoes.ContratoVer,
    ];

    private readonly ITenantContext _contexto;
    private readonly IConsultasAgenda _consultas;
    private readonly ICalendarios _calendarios;
    private readonly RelogioDoTenant _relogio;

    public LeituraAgenda(ITenantContext contexto, IConsultasAgenda consultas, ICalendarios calendarios, RelogioDoTenant relogio)
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

    /// <param name="responsavelId">Só itens da pessoa: projetos e contratos (sem responsável) ficam de fora.</param>
    public async Task<AgendaDto> ObterAsync(
        DateOnly? de, DateOnly? ate, Guid? clienteId, Guid? responsavelId, CancellationToken cancellationToken = default)
    {
        if (!PermissoesDeLeitura.Any(Pode))
            throw new AcessoNegadoException("Sem permissão para ver a agenda.");

        var hoje = await _relogio.HojeAsync(cancellationToken);
        var segunda = hoje.AddDays(-(((int)hoje.DayOfWeek + 6) % 7));
        var inicio = de ?? segunda;
        var fim = ate ?? (de is null ? segunda.AddDays(13) : inicio.AddDays(13));
        if (fim < inicio)
            throw ErroDeCampo.Criar("ate", "O fim do período não pode ser anterior ao início.");
        if (fim.DayNumber - inicio.DayNumber + 1 > MaximoDias)
            throw ErroDeCampo.Criar("ate", $"O período vai até {MaximoDias} dias.");

        var calendario = await _calendarios.DoTenantAsync(cancellationToken);
        var feriados = calendario.Feriados.ToHashSet();
        var dias = new List<DiaAgendaDto>(fim.DayNumber - inicio.DayNumber + 1);
        for (var dia = inicio; dia <= fim; dia = dia.AddDays(1))
            dias.Add(new DiaAgendaDto(dia, calendario.JanelaDe(dia) is not null, feriados.Contains(dia)));

        var comHoje = inicio <= hoje && hoje <= fim;
        var filtro = new FiltroAgenda(clienteId, responsavelId);
        var itens = new List<ItemAgendaDto>();

        if (Pode(Permissoes.SolicitacaoVer))
        {
            var agora = _relogio.Agora;
            var antes = Instante(fim.AddDays(1), calendario.Fuso);
            foreach (var s in await _consultas.SolicitacoesAsync(antes, filtro, cancellationToken))
            {
                var dia = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(s.PrazoResolucao, calendario.Fuso).DateTime);
                var situacao = Situacao(s, agora);
                if (dia < inicio && !(comHoje && situacao == SituacaoItemAgenda.Violado))
                    continue;
                itens.Add(new ItemAgendaDto(
                    TipoItemAgenda.Solicitacao, s.Id, s.Titulo, dia, dia, situacao, s.Responsavel, s.Cliente, Numero: s.Numero));
            }
        }

        if (Pode(Permissoes.TarefaVer))
        {
            foreach (var t in await _consultas.TarefasAsync(inicio, fim, comHoje, filtro, cancellationToken))
            {
                var dia = (t.PlanejadaPara ?? t.Prazo)!.Value;
                var situacao = t.Status == StatusTarefa.Concluida ? SituacaoItemAgenda.Concluido
                    : t.Prazo < hoje ? SituacaoItemAgenda.Vencido
                    : SituacaoItemAgenda.NoPrazo;
                itens.Add(new ItemAgendaDto(
                    TipoItemAgenda.Tarefa, t.Id, t.Titulo, dia, dia, situacao, t.Responsavel, t.Cliente,
                    Prazo: t.Prazo, EstimativaMinutos: t.EstimativaMinutos, SolicitacaoId: t.SolicitacaoId, ProjetoId: t.ProjetoId));
            }
        }

        if (responsavelId is null && Pode(Permissoes.ProjetoVer))
        {
            foreach (var p in await _consultas.ProjetosAsync(inicio, fim, comHoje, clienteId, cancellationToken))
            {
                var comeca = (p.Inicio ?? p.Prazo)!.Value;
                var termina = (p.Prazo ?? p.Inicio)!.Value;
                var situacao = p.Prazo < hoje ? SituacaoItemAgenda.Vencido : SituacaoItemAgenda.NoPrazo;
                itens.Add(new ItemAgendaDto(TipoItemAgenda.Projeto, p.Id, p.Nome, comeca, termina, situacao, null, p.Cliente, Prazo: p.Prazo));
            }
        }

        if (Pode(Permissoes.RecorrenciaVer))
        {
            foreach (var r in await _consultas.RecorrenciasAsync(fim, clienteId, cancellationToken))
            {
                if (responsavelId is not null && r.Responsavel?.Id != responsavelId)
                    continue;
                var desde = r.ProximaData > inicio ? r.ProximaData : inicio;
                foreach (var data in GeradorDeDatas.Ocorrencias(r.Regra, desde, fim))
                {
                    itens.Add(new ItemAgendaDto(
                        TipoItemAgenda.Recorrencia, r.Id, r.Titulo, data, data, SituacaoItemAgenda.Previsto, r.Responsavel, r.Cliente,
                        EstimativaMinutos: r.EstimativaMinutos));
                }
            }
        }

        if (responsavelId is null && Pode(Permissoes.ContratoVer))
        {
            foreach (var c in await _consultas.ContratosAsync(inicio, fim, hoje, clienteId, cancellationToken))
            {
                if (c.FimVigencia is { } termino && inicio <= termino && termino <= fim)
                {
                    itens.Add(new ItemAgendaDto(
                        TipoItemAgenda.Contrato, c.Id, "Fim da vigência do contrato", termino, termino, SituacaoItemAgenda.Previsto,
                        null, c.Cliente, Marco: MarcoContrato.FimVigencia));
                }

                if (comHoje && c.Percentual is { } percentual && percentual >= 80)
                {
                    var estourou = percentual >= 100;
                    itens.Add(new ItemAgendaDto(
                        TipoItemAgenda.Contrato, c.Id, $"Consumo do contrato em {percentual}%", hoje, hoje,
                        estourou ? SituacaoItemAgenda.Violado : SituacaoItemAgenda.EmRisco, null, c.Cliente,
                        Marco: estourou ? MarcoContrato.ConsumoEsgotado : MarcoContrato.ConsumoAlto, Percentual: percentual));
                }
            }
        }

        var equipe = await _consultas.EquipeAsync(cancellationToken);
        return new AgendaDto(
            inicio,
            fim,
            hoje,
            dias,
            equipe,
            [.. itens.OrderBy(i => i.Inicio).ThenBy(i => i.Tipo).ThenBy(i => i.Titulo, StringComparer.Ordinal).ThenBy(i => i.Id)]);
    }

    /// <summary>Mesmas regras do filtro <c>situacaoSla</c> (read side §5); a pausa vem antes.</summary>
    private static SituacaoItemAgenda Situacao(SolicitacaoAgenda s, DateTimeOffset agora)
    {
        if (s.Pausada)
            return SituacaoItemAgenda.Pausado;
        if (agora > s.PrazoResolucao || (!s.Respondida && agora > s.PrazoPrimeiraResposta))
            return SituacaoItemAgenda.Violado;
        return s.LimiteRisco <= agora ? SituacaoItemAgenda.EmRisco : SituacaoItemAgenda.NoPrazo;
    }

    private bool Pode(string permissao) => _contexto.Permissoes.Contains(permissao);

    private static DateTimeOffset Instante(DateOnly dia, TimeZoneInfo fuso)
    {
        var local = dia.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, fuso.GetUtcOffset(local)).ToUniversalTime();
    }
}
