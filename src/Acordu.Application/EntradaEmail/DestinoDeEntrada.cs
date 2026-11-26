using Acordu.Domain.Common;

namespace Acordu.Application.EntradaEmail;

/// <summary>
/// O destinatário de um e-mail recebido, lido como endereço de entrada (design do F15, D2 e D3; ADR 0016, item 3):
/// <c>{código}@{domínio}</c> (a empresa) ou <c>{código}+{token}@{domínio}</c> (a conversa de uma solicitação). Só o
/// formato é conferido aqui; quem diz se o código é de uma empresa é a consulta revisada da Infrastructure
/// (<see cref="IEmpresasDeEntrada"/>), e o token é procurado dentro dela (F15i).
/// </summary>
/// <param name="Endereco">O destinatário inteiro, aparado e em minúsculas (parte da chave de idempotência, D6).</param>
/// <param name="Codigo">O código da empresa, no formato de <see cref="CodigoDeEntrada"/>.</param>
/// <param name="Token">A parte depois do "+", como veio (o formato é conferido no F15i); nulo sem ela.</param>
public sealed record DestinoDeEntrada(string Endereco, string Codigo, string? Token)
{
    /// <summary>Teto de um endereço de e-mail (RFC 5321: 64 antes do @ e 255 no domínio).</summary>
    public const int TamanhoMaximo = 320;

    /// <summary>
    /// O destino, ou nulo quando ele não pode ser de empresa nenhuma (F15-22): vazio, sem a entrada configurada, de outro
    /// domínio ou com um código fora do formato. Maiúsculas não contam (os servidores de e-mail as trocam à vontade).
    /// </summary>
    public static DestinoDeEntrada? Ler(string? destinatario, OpcoesEmailEntrada opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);
        var endereco = destinatario?.Trim().ToLowerInvariant();
        if (opcoes.Dominio is null || string.IsNullOrEmpty(endereco) || endereco.Length > TamanhoMaximo)
            return null;

        var arroba = endereco.LastIndexOf('@');
        if (arroba <= 0 || !endereco.AsSpan(arroba + 1).Equals(opcoes.Dominio, StringComparison.Ordinal))
            return null;

        var local = endereco[..arroba];
        var mais = local.IndexOf('+', StringComparison.Ordinal);
        var codigo = mais < 0 ? local : local[..mais];
        if (!CodigoDeEntrada.Valido(codigo))
            return null;

        var token = mais < 0 ? null : local[(mais + 1)..];
        return new DestinoDeEntrada(endereco, codigo, string.IsNullOrEmpty(token) ? null : token);
    }
}
