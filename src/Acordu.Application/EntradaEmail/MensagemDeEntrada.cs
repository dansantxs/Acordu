namespace Acordu.Application.EntradaEmail;

/// <summary>
/// O que a entrada usa de um e-mail recebido, já lido do MIME pela Infrastructure (design do F15, D1 e D14), mais o que o
/// adaptador do provedor manda à parte (envelope e vereditos do filtro e da autenticidade, D11): a identificação, os sinais
/// dos descartes (F15d, F15e) e o assunto e o texto da abertura (F15f); os PRs dos anexos, da resposta e da triagem
/// acrescentam o resto. Nada daqui vai ao log (convenções §5).
/// </summary>
/// <param name="MessageId">O <c>Message-ID</c>, sem os sinais de menor e maior; nulo quando o e-mail não traz.</param>
/// <param name="Remetentes">Os endereços do <c>From</c>, como vieram (a identificação usa só quando há um, F15f).</param>
/// <param name="HashDoConteudo">SHA-256 (hex minúsculo) do MIME inteiro, a chave quando não há <c>Message-ID</c> (D6).</param>
/// <param name="Sinais">Os cabeçalhos que marcam o e-mail automático (D12, F15-85); nulo = nenhum.</param>
/// <param name="RemetenteDoEnvelope">
/// O remetente do envelope (<c>X-Email-Remetente</c>): vazio ou <c>&lt;&gt;</c> é devolução (D12); nulo = o adaptador não
/// mandou (sem esse sinal).
/// </param>
/// <param name="Filtro">O veredito do filtro do provedor (<c>X-Email-Filtro</c>: <c>ok</c>, <c>spam</c>, <c>virus</c>; D11).</param>
/// <param name="Assunto">O <c>Subject</c> decodificado, como veio; nulo sem ele (F15-11, F15-12).</param>
/// <param name="Texto">
/// O corpo só como texto (D14): a parte <c>text/plain</c> ou, sem ela, o HTML convertido em texto; nulo sem corpo (F15-13).
/// </param>
/// <param name="Autenticidade">
/// O veredito de autenticidade do adaptador (<c>X-Email-Autenticidade</c>: <c>pass</c>, <c>fail</c>, <c>none</c>; D11); só
/// <c>pass</c> deixa o remetente ser conhecido (F15-26, F15-27). Nulo sem o cabeçalho.
/// </param>
public sealed record MensagemDeEntrada(
    string? MessageId,
    IReadOnlyList<string> Remetentes,
    string HashDoConteudo,
    SinaisDeAutomatico? Sinais = null,
    string? RemetenteDoEnvelope = null,
    string? Filtro = null,
    string? Assunto = null,
    string? Texto = null,
    string? Autenticidade = null);

/// <summary>
/// Os cabeçalhos do e-mail que a lista do design (D12) usa para reconhecer o e-mail automático, como vieram (aparados).
/// </summary>
/// <param name="AutoSubmitted">O <c>Auto-Submitted</c> (RFC 3834); automático quando diferente de <c>no</c>.</param>
/// <param name="Precedence">O <c>Precedence</c>; automático em <c>bulk</c>, <c>junk</c> ou <c>list</c>.</param>
/// <param name="ListaDeDistribuicao">Há <c>List-Id</c> ou <c>List-Unsubscribe</c> (mensagem em massa).</param>
/// <param name="RespostaAutomatica">Há <c>X-Autoreply</c> ou <c>X-Autorespond</c>.</param>
/// <param name="TipoDoCorpo">O tipo MIME do corpo (<c>multipart/report</c> é aviso de entrega).</param>
public sealed record SinaisDeAutomatico(
    string? AutoSubmitted = null,
    string? Precedence = null,
    bool ListaDeDistribuicao = false,
    bool RespostaAutomatica = false,
    string? TipoDoCorpo = null);
