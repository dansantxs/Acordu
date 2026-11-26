using System.Collections.Frozen;
using Acordu.Application.Abstractions;

namespace Acordu.Application.Notificacoes.Canais;

/// <summary>
/// Os avisos ao contato por e-mail (RF-NOT-06; design do F15, D7 e D13):
/// <list type="bullet">
/// <item>com o recebimento ligado (o aviso traz o endereço da conversa), o Reply-To é o endereço da conversa, e a
/// resposta do contato entra na solicitação (F15-51); o texto começa pela <see cref="LinhaMarcadora"/> (F15-57);</item>
/// <item>com ele desligado, o Reply-To é o "responder para" da empresa, se houver, e o texto é o de sempre (F15-52);</item>
/// <item>sempre, os cabeçalhos de mensagem automática (<see cref="CabecalhosAutomaticos"/>, F15-55).</item>
/// </list>
/// O assunto e o fio da origem EMAIL (D4) entram com a abertura por e-mail (F15f).
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
        var texto = conversa is null ? aviso.Texto : $"{LinhaMarcadora}\n\n{aviso.Texto}";
        await _email.EnviarAsync(
            new MensagemEmail(
                aviso.EmailContato, aviso.Assunto, texto, aviso.Categoria, aviso.Prestador, conversa ?? aviso.ResponderPara,
                CabecalhosAutomaticos),
            cancellationToken);
        return true;
    }
}
