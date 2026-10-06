using Acordu.Domain.Enums;

namespace Acordu.Application.Contratos;

/// <summary>Item da listagem de contratos.</summary>
public sealed record ContratoResumoDto(
    Guid Id, Guid ClienteId, string ClienteNome, TipoContrato Tipo, DateOnly Inicio, DateOnly? Fim, int? FranquiaMinutos,
    StatusContrato Status);

/// <summary>Detalhe do contrato com SLA e o consumo do mês corrente (contrato §5).</summary>
public sealed record ContratoDto(
    Guid Id,
    Guid ClienteId,
    string ClienteNome,
    TipoContrato Tipo,
    DateOnly Inicio,
    DateOnly? Fim,
    int? FranquiaMinutos,
    StatusContrato Status,
    IReadOnlyList<PrazoSlaDto> Sla,
    IReadOnlyList<string> PeriodosFechados,
    string Versao,
    ConsumoContratoDto ConsumoAtual);

/// <summary>Prazos de uma prioridade, em minutos úteis.</summary>
public sealed record PrazoSlaDto(Prioridade Prioridade, int PrimeiraRespostaMinutos, int ResolucaoMinutos);

/// <summary>Ajuste de saldo registrado.</summary>
public sealed record AjusteSaldoDto(Guid Id, Guid ContratoId, int QuantidadeMinutos, string Motivo, DateOnly Data, Guid AutorId);

/// <summary>Corpo do <c>POST /contratos</c>.</summary>
public sealed record NovoContratoRequest(
    Guid ClienteId, TipoContrato Tipo, DateOnly Inicio, DateOnly? Fim, int? FranquiaMinutos, IReadOnlyList<PrazoSlaDto>? Sla);

/// <summary>Corpo do <c>PATCH /contratos/{id}</c>: todos os campos editáveis (ausente ou nulo apaga franquia/fim).</summary>
public sealed record EdicaoContratoRequest(int? FranquiaMinutos, DateOnly? Fim, IReadOnlyList<PrazoSlaDto>? Sla);

/// <summary>Corpo opcional do <c>POST /contratos/{id}/encerramento</c>; sem data, encerra hoje (fuso do tenant).</summary>
public sealed record EncerramentoRequest(DateOnly? Data);

/// <summary>Corpo do <c>POST /contratos/{id}/ajustes</c>; sem data, vale hoje.</summary>
public sealed record AjusteSaldoRequest(int QuantidadeMinutos, string Motivo, DateOnly? Data);
