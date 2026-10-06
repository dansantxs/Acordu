using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Acordu.Domain.Common;

/// <summary>
/// Token aleatório que vai por e-mail (link de acesso, convite do portal): 32 bytes de CSPRNG em base64url; no banco fica
/// só o SHA-256 em hexadecimal minúsculo (segurança §5.3).
/// </summary>
public static class TokenSeguro
{
    private const int Bytes = 32;

    public static string Gerar() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(Bytes));

    public static string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    /// <summary>O hash do token confere com o guardado, em tempo constante.</summary>
    public static bool Confere(string? token, string? hashGuardado) =>
        !string.IsNullOrEmpty(token)
        && !string.IsNullOrEmpty(hashGuardado)
        && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(token)), Encoding.ASCII.GetBytes(hashGuardado));
}
