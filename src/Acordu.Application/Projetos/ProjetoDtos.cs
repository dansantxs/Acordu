using Acordu.Application.Common;
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

public sealed record NovoProjetoRequest(
    Guid ClienteId, string Nome, Guid? ContratoId, DateOnly? Prazo, int? EstimativaMinutos, DateOnly? Inicio = null);

/// <summary>Corpo do <c>PATCH /projetos/{id}</c>: o formulário inteiro (trocar o contrato não reescreve apontamentos).</summary>
public sealed record EdicaoProjetoRequest(
    string Nome, Guid? ContratoId, DateOnly? Prazo, int? EstimativaMinutos, DateOnly? Inicio = null);
