namespace Acordu.Application.Autenticacao;

/// <summary>
/// Desafio contra automação nas rotas públicas que gravam (RNF-SEG-10; spec do F9, decisão 5): Cloudflare Turnstile,
/// conferido no servidor por HTTP, sem SDK. Cada rota tem a sua ação (<see cref="AcoesVerificacaoHumana"/>): um token
/// tirado para uma não vale na outra. Desligado sem segredo configurado (dev, testes, E2E): aprova tudo sem chamar ninguém.
/// </summary>
public interface IVerificacaoHumana
{
    /// <param name="acao">A ação que o token precisa trazer (<see cref="AcoesVerificacaoHumana"/>).</param>
    Task<ResultadoVerificacaoHumana> VerificarAsync(string? token, string? ip, string acao, CancellationToken cancellationToken = default);
}

/// <summary>Ações do Turnstile: o widget da tela manda a mesma (<c>action</c>), e o servidor confere.</summary>
public static class AcoesVerificacaoHumana
{
    /// <summary>Cadastro (<c>POST /auth/registro</c>, F9c).</summary>
    public const string Registro = "registro";

    /// <summary>Lista de espera (<c>POST /lista-espera</c>, F14o; design do F14, D12).</summary>
    public const string ListaEspera = "lista-espera";
}

public enum ResultadoVerificacaoHumana
{
    Aprovada = 1,

    /// <summary>Sem token, ou o Cloudflare recusou (token inválido, vencido, de outro site ou de outra ação).</summary>
    Recusada = 2,

    /// <summary>O Cloudflare não respondeu a tempo ou respondeu erro: o envio é recusado mesmo assim (fail-closed, 5c).</summary>
    Indisponivel = 3,
}
