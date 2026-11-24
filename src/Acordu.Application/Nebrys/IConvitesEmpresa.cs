using Acordu.Domain.Entities;

namespace Acordu.Application.Nebrys;

/// <summary>
/// Convites de empresa vistos pelas ações da área da Nebrys (F14-72 a F14-74; design D12). Tabela global, fora dos
/// tenants (ADR 0014, item 7): só a área, as consultas dela e o cadastro (pela classe da Infrastructure) usam o tipo.
/// Grava no próximo <c>SalvarAsync</c> da unidade de trabalho, com a trilha.
/// </summary>
public interface IConvitesEmpresa
{
    void Adicionar(ConviteEmpresa convite);

    /// <summary>O convite, rastreado para a gravação; nulo se não existe.</summary>
    Task<ConviteEmpresa?> ObterAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Se o e-mail (já normalizado) tem um convite nem usado nem revogado, vencido ou não.</summary>
    Task<bool> HaEmAbertoAsync(string emailNormalizado, CancellationToken cancellationToken = default);
}
