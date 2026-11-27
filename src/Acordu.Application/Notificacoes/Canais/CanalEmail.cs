using System.Collections.Frozen;
using System.Globalization;
using Acordu.Application.Abstractions;
using Acordu.Domain.Enums;

namespace Acordu.Application.Notificacoes.Canais;

/// <summary>
/// Os avisos ao contato por e-mail (RF-NOT-06; design do F15, D7 e D13):
/// <list type="bullet">
/// <item>com o recebimento ligado (o aviso traz o endereço da conversa), o Reply-To é o endereço da conversa, e a
/// resposta do contato entra na solicitação (F15-51); o texto começa pela <see cref="LinhaMarcadora"/> (F15-57);</item>
/// <item>com ele desligado, o Reply-To é o "responder para" da empresa, se houver, e o texto é o de sempre (F15-52);</item>
/// <item>sempre, os cabeçalhos de mensagem automática (<see cref="CabecalhosAutomaticos"/>, F15-55);</item>
/// <item>na origem EMAIL com o fio guardado (D4; F15-16, F15-50), o assunto <c>Re: {assunto original} (#N)</c>, o assunto de
/// sempre como a primeira linha do texto (depois da marcadora, se houver) e <c>In-Reply-To</c> e <c>References</c> com o
/// <c>Message-ID</c> do e-mail que abriu a solicitação, para o programa do contato mostrar o aviso na mesma conversa.</item>
/// </list>
/// </summary>
public sealed class CanalEmail : ICanalDeAviso
{
    /// <summary>
    /// A linha no topo dos avisos com o recebimento ligado (D13): a resposta guarda o que vem acima dela (F15-46, no F15j).
    /// </summary>
    public const string LinhaMarcadora = "— Escreva sua resposta acima desta linha —";

    /// <summary>
    /// <c>Auto-Submitted: auto-generated</c> (RFC 3834), que os programas de resposta automática respeitam, e
    /// <c>X-Auto-Response-Suppress</c>, o equivalente do Exchange e do Outlook para ausência e resposta automática.
    /// </summary>
    public static readonly FrozenDictionary<string, string> CabecalhosAutomaticos =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Auto-Submitted"] = "auto-generated",
        ["X-Auto-Response-Suppress"] = "OOF, AutoReply",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private readonly IEnviadorEmail _email;

    public CanalEmail(IEnviadorEmail email)
    {
        ArgumentNullException.ThrowIfNull(email);
        _email = email;
    }

    public CanalAviso Canal => CanalAviso.Email;

    public async Task<bool> EnviarAsync(AvisoAoContato aviso, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aviso);
        var conversa = aviso.EnderecoDaConversa;
        var noFio = aviso.Origem == OrigemSolicitacao.Email && aviso.AssuntoOrigem is not null;
        var assunto = noFio ? AssuntoNoFio(aviso.AssuntoOrigem!, aviso.NumeroSolicitacao) : aviso.Assunto;
        var corpo = noFio ? $"{aviso.Assunto}\n\n{aviso.Texto}" : aviso.Texto;
        var texto = conversa is null ? corpo : $"{LinhaMarcadora}\n\n{corpo}";
        await _email.EnviarAsync(
            new MensagemEmail(
                aviso.EmailContato, assunto, texto, aviso.Categoria, aviso.Prestador, conversa ?? aviso.ResponderPara,
                Cabecalhos(noFio ? aviso.MessageIdOrigem : null)),
            cancellationToken);
        return true;
    }

    /// <summary><c>Re: {assunto original} (#N)</c> (D4); sem assunto original, <c>Re: (#N)</c>.</summary>
    public static string AssuntoNoFio(string assuntoOrigem, int numero)
    {
        ArgumentNullException.ThrowIfNull(assuntoOrigem);
        var original = assuntoOrigem.Trim();
        return original.Length == 0
            ? string.Create(CultureInfo.InvariantCulture, $"Re: (#{numero})")
            : string.Create(CultureInfo.InvariantCulture, $"Re: {original} (#{numero})");
    }

    /// <summary>Os de mensagem automática e, com o <c>Message-ID</c> de origem, os de resposta a ele (RFC 5322 §3.6.4).</summary>
    private static IReadOnlyDictionary<string, string> Cabecalhos(string? messageIdOrigem)
    {
        if (string.IsNullOrWhiteSpace(messageIdOrigem))
            return CabecalhosAutomaticos;
        var id = $"<{messageIdOrigem.Trim()}>";
        return new Dictionary<string, string>(CabecalhosAutomaticos, StringComparer.OrdinalIgnoreCase)
        {
            ["In-Reply-To"] = id,
            ["References"] = id,
        };
    }
}
