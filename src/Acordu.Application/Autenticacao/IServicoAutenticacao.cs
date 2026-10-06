namespace Acordu.Application.Autenticacao;

/// <summary>
/// Identidade e sessões (segurança §4–5): registro, login, renovação, troca de tenant e logout. A Api transforma a
/// <see cref="Sessao"/> em access token (JWT) e o refresh token em cookie.
/// </summary>
public interface IServicoAutenticacao
{
    /// <summary>
    /// Cria usuário, tenant, papéis padrão, vínculo Dono e calendário padrão numa transação, e já abre a sessão.
    /// Entrada inválida → <see cref="ArgumentException"/> com o campo.
    /// </summary>
    Task<Sessao> RegistrarAsync(string nome, string email, string senha, string nomeEmpresa, CancellationToken cancellationToken = default);

    /// <summary>Nulo para credenciais inválidas, conta bloqueada ou usuário sem vínculo, sem distinguir o motivo.</summary>
    Task<Sessao?> EntrarAsync(string email, string senha, CancellationToken cancellationToken = default);

    /// <summary>Troca o refresh token por uma sessão nova (rotação). Nulo se inválido; reuso revoga a família.</summary>
    Task<Sessao?> RenovarAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>Abre sessão em outro tenant do usuário, revogando o refresh atual. Nulo se não há vínculo nele.</summary>
    Task<Sessao?> TrocarTenantAsync(Guid usuarioId, Guid tenantId, string? refreshAtual, CancellationToken cancellationToken = default);

    /// <summary>Revoga o refresh token, se existir e estiver ativo.</summary>
    Task SairAsync(string? refreshToken, CancellationToken cancellationToken = default);

    /// <summary>Dados do <c>/auth/eu</c>, lidos do banco (não do token). Nulo se o vínculo não existe mais.</summary>
    Task<PerfilUsuario?> PerfilAsync(Guid usuarioId, Guid tenantId, CancellationToken cancellationToken = default);
}
