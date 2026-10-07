namespace Acordu.Application.AcessosPortal;

/// <summary>
/// Corpo do <c>POST /solicitacoes/{id}/acessos-portal</c> (contrato §7). Validade de 1 a 90 dias (padrão 30);
/// <see cref="EnviarEmail"/> nulo envia.
/// </summary>
public sealed record EmissaoLinkRequest(Guid ContatoId, int? ValidadeDias, bool? EnviarEmail);

/// <summary>Resposta da emissão: o link (com o token) só aparece aqui, uma vez.</summary>
public sealed record LinkEmitidoDto(Guid AcessoId, DateTimeOffset ExpiraEm, Uri Link, bool EmailEnviado);

/// <summary>Link ainda válido da solicitação (para listar e revogar), sem o token.</summary>
public sealed record LinkPortalDto(Guid AcessoId, Guid ContatoId, string ContatoNome, DateTimeOffset EmitidoEm, DateTimeOffset ExpiraEm);

/// <summary>Links válidos (não revogados nem expirados) de uma solicitação; nulo se ela não é visível.</summary>
public interface IConsultasLinkPortal
{
    Task<IReadOnlyList<LinkPortalDto>?> ListarAsync(Guid solicitacaoId, DateTimeOffset agora, CancellationToken cancellationToken = default);
}
