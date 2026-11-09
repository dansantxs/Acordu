using Acordu.Application.Anexos;
using Acordu.Application.Common;
using Acordu.Domain.Enums;

namespace Acordu.Application.Solicitacoes;

/// <summary>Item da fila de solicitações.</summary>
public sealed record SolicitacaoResumoDto(
    Guid Id,
    int Numero,
    string Titulo,
    Referencia Cliente,
    Prioridade Prioridade,
    StatusSolicitacao Status,
    StatusExterno StatusExterno,
    Referencia? Responsavel,
    DateTimeOffset AbertaEm,
    DateTimeOffset? PrazoResolucao,
    SituacaoSla SituacaoSla);

/// <summary>Detalhe da solicitação (contrato §7), com o SLA calculado no instante da consulta.</summary>
public sealed record SolicitacaoDto(
    Guid Id,
    int Numero,
    string Versao,
    string Titulo,
    string? Descricao,
    Referencia Cliente,
    Referencia? Contato,
    Guid? ContratoId,
    Guid? CategoriaId,
    Prioridade Prioridade,
    StatusSolicitacao Status,
    StatusExterno StatusExterno,
    OrigemSolicitacao Origem,
    Referencia? Responsavel,
    DateTimeOffset AbertaEm,
    SlaDto? Sla,
    SituacaoSla SituacaoSla,
    int TempoApontadoMinutos,
    IReadOnlyList<TarefaDaSolicitacaoDto> Tarefas,
    SolucaoDto? Solucao,
    DateTimeOffset? ResolvidaEm,
    DateTimeOffset? FechadaEm,
    DateTimeOffset? CanceladaEm,
    string? MotivoCancelamento);


/// <summary>Prazos e situação do SLA; nulo quando a solicitação não tem contrato.</summary>
public sealed record SlaDto(
    DateTimeOffset PrazoPrimeiraResposta,
    DateTimeOffset? PrimeiraRespostaEm,
    DateTimeOffset PrazoResolucao,
    bool Pausado,
    SituacaoSla Situacao,
    int PercentualConsumido);

public sealed record TarefaDaSolicitacaoDto(Guid Id, string Titulo, StatusTarefa Status);

public sealed record SolucaoDto(string? Problema, string? Causa, string Texto);

/// <summary>Item da linha do tempo (internas e externas).</summary>
public sealed record InteracaoDto(
    Guid Id,
    Referencia Autor,
    bool AutorEhCliente,
    string? Texto,
    Visibilidade Visibilidade,
    DateTimeOffset RegistradaEm,
    IReadOnlyList<AnexoDto> Anexos);

/// <summary>Corpo do <c>POST /solicitacoes</c> (origem INTERNA).</summary>
public sealed record NovaSolicitacaoRequest(
    Guid ClienteId,
    Guid? ContatoId,
    Guid? ContratoId,
    string Titulo,
    string? Descricao,
    Prioridade Prioridade,
    Guid? CategoriaId,
    Guid? ResponsavelId);

/// <summary>
/// Corpo do <c>PATCH /solicitacoes/{id}</c>: o formulário inteiro. Trocar o contrato (com recálculo de SLA) fica
/// para um marco posterior (modelo §4.3).
/// </summary>
public sealed record EdicaoSolicitacaoRequest(
    string Titulo, string? Descricao, Prioridade Prioridade, Guid? CategoriaId, Guid? ResponsavelId);

/// <summary>
/// O que fazer com as tarefas abertas (A_FAZER ou FAZENDO) junto com a transição (F11-11 a F11-14, F11-20).
/// <see cref="Ids"/> são as que a pessoa viu: se a lista mudou no meio, <c>409 conflito-versao</c> e nada muda (F11-31).
/// </summary>
public sealed record TarefasAbertasRequest(AcaoTarefasAbertas Acao, IReadOnlyList<Guid>? Ids);

public enum AcaoTarefasAbertas
{
    Manter = 0,
    Concluir = 1,
    Cancelar = 2,
}

/// <summary>Resolver; <see cref="Tarefas"/>: só <c>MANTER</c> (padrão) ou <c>CONCLUIR</c>.</summary>
public sealed record ResolucaoRequest(SolucaoDto? Solucao, TarefasAbertasRequest? Tarefas = null);

/// <summary>Cancelar; <see cref="Tarefas"/>: só <c>MANTER</c> (padrão) ou <c>CANCELAR</c>.</summary>
public sealed record CancelamentoRequest(string Motivo, TarefasAbertasRequest? Tarefas = null);

public sealed record MotivoRequest(string Motivo);

/// <summary>
/// Corpo do <c>POST /solicitacoes/{id}/interacoes</c>. Só texto por enquanto (JSON); com anexos, vira multipart no
/// marco de anexos. Visibilidade padrão: INTERNA.
/// </summary>
/// <summary>
/// Corpo do <c>POST /solicitacoes/{id}/interacoes</c>. <see cref="AguardarCliente"/> (F11a): resposta ao cliente que já
/// põe a solicitação em AGUARDANDO_CLIENTE; exige também <c>solicitacao.editar</c>.
/// </summary>
public sealed record InteracaoRequest(string? Texto, Visibilidade? Visibilidade, bool? AguardarCliente = null);

/// <summary>Corpo do <c>PUT /solicitacoes/{id}/contrato</c>; nulo remove o contrato (e o SLA).</summary>
public sealed record TrocaContratoRequest(Guid? ContratoId);

/// <summary>Filtros do <c>GET /solicitacoes</c> (contrato §7).</summary>
public sealed record FiltroSolicitacoes(
    IReadOnlyList<StatusSolicitacao>? Status = null,
    Guid? ClienteId = null,
    Guid? ResponsavelId = null,
    Prioridade? Prioridade = null,
    Guid? CategoriaId = null,
    SituacaoSla? SituacaoSla = null,
    string? Busca = null,
    DateOnly? AbertaDe = null,
    DateOnly? AbertaAte = null,
    string? Ordem = null,
    bool SemResponsavel = false);
