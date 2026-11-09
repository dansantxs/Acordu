using Acordu.Domain.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.Events;

/// <summary>Tarefa concluída. (v1) nada obrigatório; (v2) sugerir resolver a solicitação.</summary>
public sealed record TarefaConcluida(
    Guid TarefaId, Guid TenantId, Guid? ClienteId, Guid? SolicitacaoId, Guid? ProjetoId) : IEventoDominio;

/// <summary>Tarefa criada (pela equipe ou por recorrência). Reação: seguimento automático do responsável (RF-COL-03).</summary>
// SolicitacaoId (F11-18, F11-19): a tarefa da solicitação a põe EM_ANDAMENTO; opcional para a outbox antiga.
public sealed record TarefaCriada(Guid TarefaId, Guid TenantId, Guid? ResponsavelId, Guid? SolicitacaoId = null) : IEventoDominio;

/// <summary>Responsável trocado. Reação: o novo responsável passa a seguir a tarefa (RF-COL-03).</summary>
public sealed record TarefaAtribuida(Guid TarefaId, Guid TenantId, Guid? ResponsavelId, Guid? Anterior) : IEventoDominio;

/// <summary>
/// Toda transição de status. <see cref="Ocorrencia"/> distingue transições repetidas (concluir, reabrir, concluir) na
/// idempotência do aviso aos seguidores (RF-COL-03).
/// </summary>
public sealed record TarefaStatusAlterada(
    Guid TarefaId, Guid TenantId, StatusTarefa De, StatusTarefa Para, Guid Ocorrencia, Guid? SolicitacaoId = null)
    : IEventoDominio;

/// <summary>Tarefa mudou de projeto (F11-26); nulo = sem projeto. Reação: linha na atividade da tarefa.</summary>
public sealed record TarefaProjetoAlterado(Guid TarefaId, Guid TenantId, Guid? De, Guid? Para) : IEventoDominio;

/// <summary>Comentário registrado numa tarefa. Reação: seguir (o autor) e avisar quem segue (RF-COL-03).</summary>
public sealed record ComentarioTarefaRegistrado(Guid ComentarioId, Guid TenantId, Guid TarefaId, Guid AutorId) : IEventoDominio;

/// <summary>Reação: recalcular o consumo do contrato no mês de <see cref="Data"/> e checar limiares (RF-CTR-07).</summary>
public sealed record ApontamentoRegistrado(
    Guid ApontamentoId, Guid TenantId, Guid? ClienteId, Guid? ContratoId, DateOnly Data, Duracao Duracao, bool Faturavel)
    : IEventoDominio;

/// <summary>Alterado: a data pode ter mudado de mês, então os dois meses precisam ser recalculados.</summary>
public sealed record ApontamentoAlterado(
    Guid ApontamentoId, Guid TenantId, Guid? ClienteId, Guid? ContratoId, DateOnly DataAnterior, DateOnly Data)
    : IEventoDominio;

/// <summary>Registrado antes da exclusão, que é feita pela Application.</summary>
public sealed record ApontamentoExcluido(
    Guid ApontamentoId, Guid TenantId, Guid? ClienteId, Guid? ContratoId, DateOnly Data) : IEventoDominio;

/// <summary>Ajuste de saldo lançado. Reação: recalcular o consumo do contrato e checar limiares (RF-CTR-07).</summary>
public sealed record AjusteSaldoRegistrado(Guid AjusteId, Guid TenantId, Guid ClienteId, Guid ContratoId, DateOnly Data)
    : IEventoDominio;
