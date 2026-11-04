namespace Acordu.Application.Autenticacao;

/// <summary>
/// Desafio contra automação no cadastro (RNF-SEG-10; spec do F9, decisão 5): Cloudflare Turnstile, conferido no servidor
/// por HTTP, sem SDK. Desligado sem segredo configurado (dev, testes, E2E): aprova tudo sem chamar ninguém.
/// </summary>
public interface IVerificacaoHumana
{
    Task<ResultadoVerificacaoHumana> VerificarAsync(string? token, string? ip, CancellationToken cancellationToken = default);
}

public enum ResultadoVerificacaoHumana
{
    Aprovada = 1,

    /// <summary>Sem token, ou o Cloudflare recusou (token inválido, vencido, de outro site ou de outra ação).</summary>
    Recusada = 2,

    /// <summary>O Cloudflare não respondeu a tempo ou respondeu erro: o cadastro é recusado mesmo assim (fail-closed, 5c).</summary>
    Indisponivel = 3,
}
