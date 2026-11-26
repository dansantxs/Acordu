namespace Acordu.Application.EntradaEmail;

/// <summary>
/// O que a entrada usa de um e-mail recebido, já lido do MIME pela Infrastructure (design do F15, D1 e D14). Por ora (F15d)
/// só a identificação: os PRs da abertura, da resposta e da triagem acrescentam assunto, texto e anexos. Nada daqui vai
/// ao log (convenções §5).
/// </summary>
/// <param name="MessageId">O <c>Message-ID</c>, sem os sinais de menor e maior; nulo quando o e-mail não traz.</param>
/// <param name="Remetentes">Os endereços do <c>From</c>, como vieram (a identificação usa só quando há um, F15f).</param>
/// <param name="HashDoConteudo">SHA-256 (hex minúsculo) do MIME inteiro, a chave quando não há <c>Message-ID</c> (D6).</param>
public sealed record MensagemDeEntrada(string? MessageId, IReadOnlyList<string> Remetentes, string HashDoConteudo);
