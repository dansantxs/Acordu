using System.Security.Cryptography;
using System.Text;

namespace Acordu.Application.EntradaEmail;

/// <summary>
/// Os hashes do registro de entrada (design do F15, D6; ADR 0016, item 8), em SHA-256 hex minúsculo. Nada em claro: o
/// registro guarda só estes valores.
/// </summary>
public static class ChaveDeEntrada
{
    /// <summary>
    /// A chave de idempotência: o <c>Message-ID</c> normalizado e o destino; sem <c>Message-ID</c>, o hash do MIME inteiro
    /// e o destino. O destino entra na chave para o mesmo e-mail mandado a duas empresas (ou à empresa e a uma conversa
    /// dela) contar em cada uma (F15-21); a repetição da mesma entrega dá a mesma chave (F15-23, F15-97, F15-103).
    /// </summary>
    public static string Calcular(string? messageId, string destino, string hashDoConteudo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destino);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashDoConteudo);
        var id = NormalizarMessageId(messageId);
        var identificacao = id is null ? "conteudo:" + hashDoConteudo.Trim().ToLowerInvariant() : "message-id:" + id;
        return Sha256(identificacao + "\n" + destino.Trim().ToLowerInvariant());
    }

    /// <summary>
    /// O hash do remetente (base dos limites por remetente, F15n): o e-mail do <c>From</c> normalizado como o dos contatos.
    /// Nulo quando o <c>From</c> não tem exatamente um endereço válido (vários endereços contam como desconhecido, F15f).
    /// </summary>
    public static string? DoRemetente(IReadOnlyList<string> remetentes) =>
        IdentificacaoDoRemetente.EmailDoRemetente(remetentes) is { } email ? Sha256(email) : null;

    /// <summary>Sem os espaços e sem os sinais de menor e maior; vazio → nulo. O resto é comparado como veio.</summary>
    public static string? NormalizarMessageId(string? messageId)
    {
        var texto = messageId?.Trim().TrimStart('<').TrimEnd('>').Trim();
        return string.IsNullOrEmpty(texto) ? null : texto;
    }

    private static string Sha256(string texto) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));
}
