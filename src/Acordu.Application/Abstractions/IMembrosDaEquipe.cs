namespace Acordu.Application.Abstractions;

/// <summary>Pessoas da equipe do tenant (vínculo com papel interno), para validar responsáveis.</summary>
public interface IMembrosDaEquipe
{
    Task<bool> EhDaEquipeAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}
