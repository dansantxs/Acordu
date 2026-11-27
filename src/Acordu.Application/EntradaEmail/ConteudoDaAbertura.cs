using System.Text.RegularExpressions;
using Acordu.Domain.Common;
using Acordu.Domain.Entities;

namespace Acordu.Application.EntradaEmail;

/// <summary>
/// Título e descrição da solicitação aberta por e-mail (RF-SOL-11; F15-11 a F15-13; design do F15, §1.2 e D14), e o texto
/// da resposta a um aviso (F15-30), em funções puras: o assunto sem os prefixos de resposta e de encaminhamento do começo,
/// cortado no limite do título, e o texto do corpo cortado no limite da descrição ou da mensagem. Nada daqui vai ao log
/// (convenções §5).
/// </summary>
public static partial class ConteudoDaAbertura
{
    /// <summary>O título quando o assunto não sobra (vazio, só espaços ou só prefixos; F15-12).</summary>
    public const string SemAssunto = "E-mail sem assunto";

    /// <summary>
    /// O assunto sem os prefixos do começo (<c>Re:</c>, <c>Res:</c>, <c>Fw:</c>, <c>Fwd:</c>, <c>Enc:</c>, <c>Tr:</c>,
    /// <c>Aw:</c>, <c>Wg:</c>, com ou sem contador como <c>Re[2]:</c>, repetidos ou não, sem diferenciar maiúsculas; F15-11),
    /// com os espaços e as quebras juntados num espaço, cortado em <see cref="LimitesTexto.Titulo"/> caracteres; sem nada
    /// que sobre, <see cref="SemAssunto"/> (F15-12).
    /// </summary>
    public static string Titulo(string? assunto)
    {
        var limpo = Prefixos().Replace(UmaLinha(assunto), string.Empty).Trim();
        return limpo.Length == 0 ? SemAssunto : Cortar(limpo, LimitesTexto.Titulo);
    }

    /// <summary>
    /// O texto do corpo (já sem HTML, D14) com as quebras de linha em <c>\n</c> e sem o caractere nulo, aparado e cortado em
    /// <see cref="LimitesTexto.Descricao"/> caracteres; corpo vazio → sem descrição (F15-13).
    /// </summary>
    public static string? Descricao(string? texto) => TextoDoCorpo(texto, LimitesTexto.Descricao);

    /// <summary>
    /// O texto da resposta a um aviso (F15-30, F15i), com a mesma limpeza da <see cref="Descricao"/>, cortado em
    /// <see cref="LimitesTexto.TextoInteracao"/> caracteres; corpo vazio → sem texto. A citação do e-mail anterior ainda
    /// fica (o corte dela é o D13, no F15j).
    /// </summary>
    public static string? TextoDaResposta(string? texto) => TextoDoCorpo(texto, LimitesTexto.TextoInteracao);

    /// <summary>
    /// O assunto original guardado na conversa para o fio dos avisos (D4): numa linha só, aparado, cortado no limite de
    /// <see cref="ConversaPorEmail.TamanhoCabecalho"/>; vazio sem assunto.
    /// </summary>
    public static string AssuntoOriginal(string? assunto) => Cortar(UmaLinha(assunto).Trim(), ConversaPorEmail.TamanhoCabecalho);

    /// <summary>
    /// O nome do remetente guardado no item da triagem (F15-70): numa linha só, aparado, cortado em
    /// <see cref="LimitesTexto.Nome"/> caracteres; nulo sem nome.
    /// </summary>
    public static string? NomeDoRemetente(string? nome)
    {
        var limpo = UmaLinha(nome).Trim();
        return limpo.Length == 0 ? null : Cortar(limpo, LimitesTexto.Nome);
    }

    /// <summary>
    /// Os primeiros <paramref name="maximo"/> caracteres, sem partir um par substituto (emoji) ao meio, e sem espaços no
    /// fim.
    /// </summary>
    internal static string Cortar(string texto, int maximo)
    {
        if (texto.Length <= maximo)
            return texto;
        var corte = char.IsHighSurrogate(texto[maximo - 1]) ? maximo - 1 : maximo;
        return texto[..corte].TrimEnd();
    }

    private static string? TextoDoCorpo(string? texto, int limite)
    {
        // O caractere nulo não cabe num texto do Postgres: sai, para um e-mail estranho não virar 5xx (e reenvio).
        var normalizado = texto?
            .Replace("\0", string.Empty, StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
        return string.IsNullOrEmpty(normalizado) ? null : Cortar(normalizado, limite);
    }

    /// <summary>Espaços, tabulações, quebras e outros caracteres de controle viram um espaço só.</summary>
    private static string UmaLinha(string? texto) => texto is null ? string.Empty : Espacos().Replace(texto, " ");

    [GeneratedRegex(@"^\s*(?:(?:re|res|fw|fwd|enc|tr|aw|wg)\s*(?:\[\d+\])?\s*:\s*)+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Prefixos();

    [GeneratedRegex(@"[\s\p{Cc}]+", RegexOptions.CultureInvariant)]
    private static partial Regex Espacos();
}
