using System.Text.Json;
using Acordu.Application.Common;

namespace Acordu.Application.Auditoria;

/// <summary>Registro da trilha de auditoria (contrato §11a). <see cref="Usuario"/> nulo: o sistema (job) ou um link do portal.</summary>
public sealed record RegistroAuditoriaDto(
    Guid Id,
    DateTimeOffset OcorridoEm,
    string Acao,
    Referencia? Usuario,
    string? AlvoTipo,
    Guid? AlvoId,
    JsonElement? Dados,
    string? Ip);

/// <summary>Filtros do <c>GET /auditoria</c>. Datas no fuso do tenant, inclusivas.</summary>
public sealed record FiltroAuditoria(DateOnly? De, DateOnly? Ate, string? Acao, Guid? UsuarioId, Guid? AlvoId);
