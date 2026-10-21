using Acordu.Application.Common;
using Acordu.Domain.Enums;

namespace Acordu.Application.Tarefas;

/// <summary>
/// Item da lista/quadro de tarefas (o kanban agrupa por <see cref="Status"/> no front). Nome do cliente, número da
/// solicitação e nome do projeto vêm junto para o cartão não precisar de outra busca (F3a, 20/10).
/// </summary>
public sealed record TarefaResumoDto(
    Guid Id,
    string Titulo,
    StatusTarefa Status,
    Guid? ClienteId,
    Guid? SolicitacaoId,
    Guid? ProjetoId,
    Referencia? Responsavel,
    DateOnly? Prazo,
    int? EstimativaMinutos,
    bool VisivelCliente,
    DateOnly? PlanejadaPara,
    string? ClienteNome = null,
    int? SolicitacaoNumero = null,
    string? ProjetoNome = null);

/// <summary>Detalhe da tarefa com checklist e total apontado (contrato §8.1).</summary>
public sealed record TarefaDto(
    Guid Id,
    string Versao,
    string Titulo,
    string? Descricao,
    StatusTarefa Status,
    Guid? ClienteId,
    Guid? SolicitacaoId,
    Guid? ProjetoId,
    Guid? RecorrenciaId,
    Referencia? Responsavel,
    DateOnly? Prazo,
    int? EstimativaMinutos,
    bool VisivelCliente,
    DateOnly? PlanejadaPara,
    int ApontadoMinutos,
    IReadOnlyList<ItemChecklistDto> Checklist);

public sealed record ItemChecklistDto(Guid Id, int Ordem, string Texto, bool Feito);

/// <summary>
/// Corpo do <c>POST /tarefas</c>. O cliente é herdado da solicitação ou do projeto; <see cref="ClienteId"/> só vale para
/// tarefa avulsa (e, se vier junto, precisa coincidir).
/// </summary>
public sealed record NovaTarefaRequest(
    string Titulo,
    string? Descricao,
    Guid? SolicitacaoId,
    Guid? ProjetoId,
    Guid? ClienteId,
    Guid? ResponsavelId,
    DateOnly? Prazo,
    int? EstimativaMinutos,
    bool VisivelCliente,
    DateOnly? PlanejadaPara = null);

/// <summary>
/// Corpo do <c>PATCH /tarefas/{id}</c>: o formulário inteiro (<see cref="PlanejadaPara"/> ausente limpa o dia planejado,
/// como os demais opcionais).
/// </summary>
public sealed record EdicaoTarefaRequest(
    string Titulo,
    string? Descricao,
    Guid? ResponsavelId,
    DateOnly? Prazo,
    int? EstimativaMinutos,
    bool VisivelCliente,
    DateOnly? PlanejadaPara = null);

/// <summary>Corpo do <c>POST</c> e do <c>PATCH</c> de item; <see cref="Feito"/> nulo mantém a marcação.</summary>
public sealed record ItemChecklistRequest(string Texto, bool? Feito);

/// <summary>Filtros do <c>GET /tarefas</c>; <see cref="Minhas"/> usa o usuário do token como responsável.</summary>
public sealed record FiltroTarefas(
    IReadOnlyList<StatusTarefa>? Status = null,
    Guid? ResponsavelId = null,
    Guid? SolicitacaoId = null,
    Guid? ProjetoId = null,
    Guid? ClienteId = null,
    DateOnly? PrazoAte = null,
    string? Busca = null,
    bool SemResponsavel = false);
