using Acordu.Domain.Common;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.Events;

/// <summary>Tarefa concluída. (v1) nada obrigatório; (v2) sugerir resolver a solicitação.</summary>
public sealed record TarefaConcluida(
    Guid TarefaId, Guid TenantId, Guid? ClienteId, Guid? SolicitacaoId, Guid? ProjetoId) : IEventoDominio;

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
