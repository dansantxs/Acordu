namespace Acordu.Application.Abstractions;

/// <summary>Pessoas da equipe (vínculo sem cliente) do tenant do contexto, para os avisos internos.</summary>
public interface IPessoasDaEquipe
{
    /// <summary>Quem tem a permissão no papel atual.</summary>
    Task<IReadOnlyList<PessoaDaEquipe>> ComPermissaoAsync(string permissao, CancellationToken cancellationToken = default);

    /// <summary>A pessoa, se ainda for da equipe deste tenant.</summary>
    Task<PessoaDaEquipe?> ObterAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}

public sealed record PessoaDaEquipe(Guid UsuarioId, string Nome, string Email);
