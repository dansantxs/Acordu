using Acordu.Application.Common;

namespace Acordu.Application.Apontamentos;

/// <summary>Apontamento de horas (contrato §9). O contrato é resolvido pela tarefa e congelado no registro (ADR 0004).</summary>
public sealed record ApontamentoDto(
    Guid Id,
    string Versao,
    Referencia Tarefa,
    Guid? ClienteId,
    Referencia Usuario,
    Guid? ContratoId,
    DateOnly Data,
    int DuracaoMinutos,
    string Descricao,
    string? DescricaoExterna,
    bool Faturavel,
    bool VisivelCliente);

/// <summary>Corpo do <c>POST /apontamentos</c>; <see cref="VisivelCliente"/> nulo segue o padrão (visível se faturável).</summary>
public sealed record NovoApontamentoRequest(
    Guid TarefaId, DateOnly Data, int DuracaoMinutos, string Descricao, string? DescricaoExterna, bool Faturavel, bool? VisivelCliente);

/// <summary>Corpo do <c>PATCH /apontamentos/{id}</c>: o formulário inteiro.</summary>
public sealed record EdicaoApontamentoRequest(
    DateOnly Data, int DuracaoMinutos, string Descricao, string? DescricaoExterna, bool Faturavel, bool VisivelCliente);

/// <summary>Filtros do <c>GET /apontamentos</c>.</summary>
public sealed record FiltroApontamentos(
    Guid? UsuarioId = null,
    Guid? ClienteId = null,
    Guid? ContratoId = null,
    Guid? TarefaId = null,
    DateOnly? De = null,
    DateOnly? Ate = null,
    bool? Faturavel = null);

/// <summary>Cronômetro ativo do usuário.</summary>
public sealed record CronometroDto(Guid Id, Referencia Tarefa, DateTimeOffset IniciadoEm, int DecorridoMinutos);

public sealed record InicioCronometroRequest(Guid TarefaId);

/// <summary>
/// Corpo opcional do <c>POST /cronometro/parada</c>: sem descrição, usa o título da tarefa; sem <see cref="Faturavel"/>,
/// faturável.
/// </summary>
public sealed record ParadaCronometroRequest(string? Descricao, string? DescricaoExterna, bool? Faturavel, bool? VisivelCliente);
