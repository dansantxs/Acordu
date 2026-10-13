using Acordu.Domain.Common;

namespace Acordu.Infrastructure.Seguranca;

/// <summary>Para que serve o token da conta.</summary>
public enum FinalidadeTokenConta
{
    ConfirmacaoEmail = 1,
    RedefinicaoSenha = 2,
}

/// <summary>
/// Token de uso único da conta (confirmação de e-mail, redefinição de senha), no mesmo padrão dos convites: 32 bytes
/// aleatórios, só o SHA-256 no banco. Global (sem tenant), como o usuário. Um novo pedido revoga os anteriores da mesma
/// finalidade. Não usa os tokens do Identity, que dependeriam das chaves do DataProtection (decidido em 09/10).
/// </summary>
public sealed class TokenConta
{
    private TokenConta(Guid usuarioId, FinalidadeTokenConta finalidade, string hash, DateTimeOffset criadoEm, DateTimeOffset expiraEm)
    {
        Id = Guid.CreateVersion7();
        UsuarioId = usuarioId;
        Finalidade = finalidade;
        Hash = hash;
        CriadoEm = criadoEm;
        ExpiraEm = expiraEm;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private TokenConta()
    {
        Hash = null!;
    }

    public Guid Id { get; private set; }

    public Guid UsuarioId { get; private set; }

    public FinalidadeTokenConta Finalidade { get; private set; }

    /// <summary>SHA-256 do token, em hexadecimal minúsculo.</summary>
    public string Hash { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset ExpiraEm { get; private set; }

    public DateTimeOffset? UsadoEm { get; private set; }

    public DateTimeOffset? RevogadoEm { get; private set; }

    /// <summary>Gera o token (só aqui em claro) e a entidade a gravar.</summary>
    public static (TokenConta Registro, string Token) Emitir(
        Guid usuarioId, FinalidadeTokenConta finalidade, DateTimeOffset agora, TimeSpan validade)
    {
        var token = TokenSeguro.Gerar();
        var utc = agora.ToUniversalTime();
        return (new TokenConta(usuarioId, finalidade, TokenSeguro.Hash(token), utc, utc + validade), token);
    }

    /// <summary>Hash confere (tempo constante), não usado, não revogado e dentro da validade.</summary>
    public bool Valido(string token, DateTimeOffset agora) =>
        UsadoEm is null && RevogadoEm is null && agora < ExpiraEm && TokenSeguro.Confere(token, Hash);

    public void Usar(DateTimeOffset agora) => UsadoEm ??= agora.ToUniversalTime();

    public void Revogar(DateTimeOffset agora)
    {
        if (UsadoEm is null)
            RevogadoEm ??= agora.ToUniversalTime();
    }
}
