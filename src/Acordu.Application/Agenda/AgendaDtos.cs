using Acordu.Application.Common;

namespace Acordu.Application.Agenda;

/// <summary>
/// Agenda da equipe num período (contrato §11b, read side §6a): os dias com feriados, a equipe (faixas da linha do tempo)
/// e os itens com data. O agrupamento por pessoa ou cliente é feito no front. Cada tipo de item só vem para quem tem a
/// permissão de leitura dele.
/// </summary>
public sealed record AgendaDto(
    DateOnly De,
    DateOnly Ate,
    DateOnly Hoje,
    IReadOnlyList<DiaAgendaDto> Dias,
    IReadOnlyList<Referencia> Equipe,
    IReadOnlyList<ItemAgendaDto> Itens);

/// <summary><see cref="Util"/> segue o calendário útil do tenant (expediente e feriados).</summary>
public sealed record DiaAgendaDto(DateOnly Data, bool Util, bool Feriado);

/// <summary>
/// Um item da agenda. Itens de um dia têm <see cref="Inicio"/> = <see cref="Fim"/>; o projeto pode ocupar vários dias.
/// <see cref="Id"/> é o da solicitação, tarefa, projeto, recorrência ou contrato, conforme o <see cref="Tipo"/>.
/// </summary>
/// <param name="Numero">Só solicitação.</param>
/// <param name="Prazo">Tarefa e projeto: o prazo combinado (a tarefa pode estar planejada para outro dia).</param>
/// <param name="EstimativaMinutos">Tarefa e recorrência (estimativa do modelo).</param>
/// <param name="SolicitacaoId">Tarefa de uma solicitação.</param>
/// <param name="ProjetoId">Tarefa de um projeto.</param>
/// <param name="Marco">Só contrato.</param>
/// <param name="Percentual">Contrato com marco de consumo: percentual do período corrente.</param>
public sealed record ItemAgendaDto(
    TipoItemAgenda Tipo,
    Guid Id,
    string Titulo,
    DateOnly Inicio,
    DateOnly Fim,
    SituacaoItemAgenda Situacao,
    Referencia? Responsavel,
    Referencia? Cliente,
    int? Numero = null,
    DateOnly? Prazo = null,
    int? EstimativaMinutos = null,
    Guid? SolicitacaoId = null,
    Guid? ProjetoId = null,
    MarcoContrato? Marco = null,
    int? Percentual = null);

public enum TipoItemAgenda
{
    Solicitacao = 1,
    Tarefa = 2,
    Projeto = 3,
    Recorrencia = 4,
    Contrato = 5,
}

/// <summary>
/// Solicitação: <see cref="NoPrazo"/>, <see cref="EmRisco"/>, <see cref="Violado"/> (regras do read side §5) ou
/// <see cref="Pausado"/> (aguardando cliente ou terceiro). Tarefa e projeto: <see cref="NoPrazo"/>, <see cref="Vencido"/>
/// (prazo antes de hoje e em aberto) ou <see cref="Concluido"/>. Recorrência e contrato: <see cref="Previsto"/>, exceto
/// o marco de consumo, que é <see cref="EmRisco"/> (80%) ou <see cref="Violado"/> (100%).
/// </summary>
public enum SituacaoItemAgenda
{
    NoPrazo = 1,
    EmRisco = 2,
    Violado = 3,
    Pausado = 4,
    Vencido = 5,
    Concluido = 6,
    Previsto = 7,
}

/// <summary>Fim da vigência no período, ou consumo do período corrente a partir de 80% (alto) ou de 100% (esgotado), no dia de hoje.</summary>
public enum MarcoContrato
{
    FimVigencia = 1,
    ConsumoAlto = 2,
    ConsumoEsgotado = 3,
}
