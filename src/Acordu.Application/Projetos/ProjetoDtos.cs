using Acordu.Application.Common;
using Acordu.Application.Tarefas;
using Acordu.Domain.Enums;

namespace Acordu.Application.Projetos;

public sealed record ProjetoResumoDto(
    Guid Id,
    string Nome,
    Referencia Cliente,
    Guid? ContratoId,
    DateOnly? Inicio,
    DateOnly? Prazo,
    int? EstimativaMinutos,
    StatusProjeto Status);

/// <summary>Detalhe do projeto com o progresso (read side §7).</summary>
public sealed record ProjetoDto(
    Guid Id,
    string Versao,
    string Nome,
    Referencia Cliente,
    Guid? ContratoId,
    DateOnly? Inicio,
    DateOnly? Prazo,
    int? EstimativaMinutos,
    StatusProjeto Status,
    ProgressoProjetoDto Progresso);

/// <summary>
/// <see cref="TarefaIds"/>: tarefas levadas para o projeto ao criar (F11-22), com as regras de mudar de projeto (F11d) e
/// <c>tarefa.editar</c>; tudo numa transação.
/// </summary>
public sealed record NovoProjetoRequest(
    Guid ClienteId,
    string Nome,
    Guid? ContratoId,
    DateOnly? Prazo,
    int? EstimativaMinutos,
    DateOnly? Inicio = null,
    IReadOnlyList<Guid>? TarefaIds = null);

/// <summary>
/// Corpo opcional de concluir ou cancelar (F11-27); <see cref="Tarefas"/>: só <c>MANTER</c> (padrão) ou a ação que
/// acompanha (<c>CONCLUIR</c> na conclusão, <c>CANCELAR</c> no cancelamento).
/// </summary>
public sealed record EncerramentoProjetoRequest(TarefasAbertasRequest? Tarefas);

/// <summary>Corpo do <c>PATCH /projetos/{id}</c>: o formulário inteiro (trocar o contrato não reescreve apontamentos).</summary>
public sealed record EdicaoProjetoRequest(
    string Nome, Guid? ContratoId, DateOnly? Prazo, int? EstimativaMinutos, DateOnly? Inicio = null);
