using Acordu.Domain.Enums;

namespace Acordu.Application.Recorrencias;

/// <summary>Molde das tarefas geradas (RF-REC-01).</summary>
public sealed record ModeloTarefaDto(
    string Titulo,
    string? Descricao,
    Guid? ResponsavelId,
    int? EstimativaMinutos,
    bool VisivelCliente,
    IReadOnlyList<string>? Checklist);

/// <summary>Quando a tarefa acontece (RF-REC-02). <c>Inicio</c> ausente: hoje (criação) ou o atual (edição).</summary>
public sealed record RegraRecorrenciaDto(
    Periodicidade Periodicidade,
    int DiaReferencia,
    int AntecedenciaDias,
    DateOnly? Inicio,
    DateOnly? Fim);

/// <summary>Prévia de uma regra ainda não salva (F4c, 21/10): as próximas datas para conferir antes de gravar.</summary>
public sealed record PreviaRegraRequest(RegraRecorrenciaDto? Regra, int? Quantidade);

/// <summary>Item da listagem de recorrências.</summary>
public sealed record RecorrenciaResumoDto(
    Guid Id,
    Guid ClienteId,
    string ClienteNome,
    Guid? ContratoId,
    string Titulo,
    Guid? ResponsavelId,
    Periodicidade Periodicidade,
    DateOnly? ProximaData,
    StatusRecorrencia Status);

/// <summary>Detalhe da recorrência (contrato §10).</summary>
public sealed record RecorrenciaDto(
    Guid Id,
    Guid ClienteId,
    string ClienteNome,
    Guid? ContratoId,
    ModeloTarefaDto Modelo,
    RegraRecorrenciaDto Regra,
    DateOnly? ProximaData,
    StatusRecorrencia Status,
    string Versao);

/// <summary>Corpo do <c>POST /recorrencias</c>.</summary>
public sealed record NovaRecorrenciaRequest(Guid ClienteId, Guid? ContratoId, ModeloTarefaDto Modelo, RegraRecorrenciaDto Regra);

/// <summary>Corpo do <c>PATCH /recorrencias/{id}</c>: contrato, modelo e regra completos (o cliente não muda).</summary>
public sealed record EdicaoRecorrenciaRequest(Guid? ContratoId, ModeloTarefaDto Modelo, RegraRecorrenciaDto Regra);
