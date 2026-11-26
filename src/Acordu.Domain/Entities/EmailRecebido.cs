using System.Buffers;
using Acordu.Domain.Common;
using Acordu.Domain.Enums;

namespace Acordu.Domain.Entities;

/// <summary>
/// Registro de entrada de um e-mail aceito para uma empresa (RF-SOL-11; design do F15, D6; ADR 0016, item 8), sem o
/// conteúdo: a chave de idempotência (<see cref="ChaveEntrada"/>, única na empresa), o hash do remetente, o instante da
/// recepção e o <see cref="Resultado"/>. Serve à idempotência (a repetição da mesma entrega esbarra no índice único) e às
/// contagens dos limites (F15n). Nada do assunto, do texto nem dos endereços em claro. Retenção curta, apagada pelo
/// diário (F15e). Raiz por tenant, sem cliente: o remetente pode nem ser contato.
/// </summary>
public sealed class EmailRecebido : EntidadeBase
{
    /// <summary>SHA-256 em hex minúsculo.</summary>
    public const int TamanhoHash = 64;

    private static readonly SearchValues<char> Hexadecimais = SearchValues.Create("0123456789abcdef");

    private EmailRecebido(Guid id, Guid tenantId)
        : base(id, tenantId)
    {
        ChaveEntrada = null!;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private EmailRecebido()
    {
        ChaveEntrada = null!;
    }

    /// <summary>SHA-256 (hex) do <c>Message-ID</c> e do destino, ou do MIME inteiro e do destino sem <c>Message-ID</c>.</summary>
    public string ChaveEntrada { get; private set; }

    /// <summary>SHA-256 (hex) do e-mail do remetente normalizado; nulo quando o <c>From</c> não tem um endereço só.</summary>
    public string? RemetenteHash { get; private set; }

    /// <summary>Instante da recepção (o do servidor, nunca o <c>Date</c> do e-mail; design D14).</summary>
    public DateTimeOffset RecebidoEm { get; private set; }

    public ResultadoEntradaEmail Resultado { get; private set; }

    public static EmailRecebido Registrar(
        Guid id, Guid tenantId, string chaveEntrada, string? remetenteHash, ResultadoEntradaEmail resultado, DateTimeOffset agora)
    {
        if (!Enum.IsDefined(resultado))
            throw new ArgumentOutOfRangeException(nameof(resultado), resultado, "Resultado desconhecido.");
        return new EmailRecebido(id, tenantId)
        {
            ChaveEntrada = Hash(chaveEntrada, nameof(chaveEntrada)),
            RemetenteHash = remetenteHash is null ? null : Hash(remetenteHash, nameof(remetenteHash)),
            Resultado = resultado,
            RecebidoEm = agora.ToUniversalTime(),
        };
    }

    /// <summary>Hex minúsculo de 64 caracteres (o formato de <c>Convert.ToHexStringLower</c> de um SHA-256).</summary>
    public static bool HashValido(string? valor) =>
        valor is { Length: TamanhoHash } && !valor.AsSpan().ContainsAnyExcept(Hexadecimais);

    private static string Hash(string valor, string parametro) =>
        HashValido(valor) ? valor : throw new ArgumentException("Esperado um SHA-256 em hex minúsculo.", parametro);
}
