namespace Acordu.Application.Nebrys;

/// <summary>
/// Sessão de operação da Nebrys (ADR 0014, itens 1 e 2): sem tenant, sem refresh. A Api transforma o operador em access
/// token de 30 min com <c>tipo = OPERACAO</c>.
/// </summary>
public interface IServicoOperacao
{
    /// <summary>
    /// Confere a senha pelo Identity (bloqueio depois de 5 falhas, como o login) e a conta: na lista de
    /// <see cref="OperadoresNebrys"/> e com o e-mail confirmado. Devolve o Id da conta, ou nulo sem distinguir o motivo
    /// (credencial errada, bloqueio, fora da lista, e-mail não confirmado). Sucesso e falha vão para a trilha, sem tenant.
    /// </summary>
    Task<Guid?> AbrirSessaoAsync(string? email, string? senha, CancellationToken cancellationToken = default);

    /// <summary>
    /// A conta continua operadora (existe, com o e-mail confirmado, e o e-mail está na lista). Conferido a cada requisição
    /// da área: tirar alguém da lista vale na revisão seguinte, sem esperar o token expirar.
    /// </summary>
    Task<bool> EhOperadorAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}

/// <summary>Corpo do <c>POST /nebrys/sessao</c>.</summary>
public sealed record SessaoOperacaoRequest(string Email, string Senha);

/// <summary>Resposta do <c>POST /nebrys/sessao</c>: access token de 30 min, sem refresh nem cookie.</summary>
public sealed record TokenDeOperacao(string AccessToken, DateTimeOffset ExpiraEm, string Tipo);
