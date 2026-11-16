using System.Security.Cryptography;
using System.Text;

namespace Acordu.Application.Cadastro;

/// <summary>
/// HMAC-SHA256 do documento normalizado (design do F14, D8; segurança §4), com a chave de <c>Documentos__ChaveHmac</c>:
/// é o que o registro global de "um teste por documento" guarda. SHA-256 puro de um CPF se desfaz por força bruta (10⁹
/// combinações); com a chave fora do banco, o hash não identifica ninguém sozinho. A chave não gira: os documentos
/// originais não ficam, então trocar a chave zera a regra para os documentos das empresas já expurgadas.
/// </summary>
public sealed class ProtecaoDocumento
{
    /// <summary>Mesmo mínimo da chave do JWT: 32 bytes (256 bits).</summary>
    public const int TamanhoMinimoChave = 32;

    private readonly byte[] _chave;

    public ProtecaoDocumento(byte[] chave)
    {
        ArgumentNullException.ThrowIfNull(chave);
        if (chave.Length < TamanhoMinimoChave)
            throw new ArgumentException($"A chave do HMAC precisa de ao menos {TamanhoMinimoChave} bytes.", nameof(chave));
        _chave = [.. chave];
    }

    /// <summary>
    /// Chave da configuração (texto em UTF-8). Ausente ou curta → <see cref="InvalidOperationException"/>: a API não sobe
    /// sem ela, para a regra não valer em silêncio com uma chave fraca.
    /// </summary>
    public static ProtecaoDocumento DaConfiguracao(string? chave) =>
        string.IsNullOrEmpty(chave) || Encoding.UTF8.GetByteCount(chave) < TamanhoMinimoChave
            ? throw new InvalidOperationException(
                $"Documentos:ChaveHmac ausente ou com menos de {TamanhoMinimoChave} bytes (Documentos__ChaveHmac).")
            : new ProtecaoDocumento(Encoding.UTF8.GetBytes(chave));

    /// <summary>
    /// HMAC do documento já normalizado pelo domínio (só letras maiúsculas e dígitos), em hexadecimal minúsculo (64
    /// caracteres). O mesmo documento escrito de jeitos diferentes chega aqui igual (F14-58).
    /// </summary>
    public string Hash(string documentoNormalizado)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentoNormalizado);
        var hmac = HMACSHA256.HashData(_chave, Encoding.UTF8.GetBytes(documentoNormalizado));
        return Convert.ToHexStringLower(hmac);
    }
}
