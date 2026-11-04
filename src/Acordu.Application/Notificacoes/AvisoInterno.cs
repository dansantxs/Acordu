using Acordu.Application.Abstractions;
using Acordu.Application.Common;

namespace Acordu.Application.Notificacoes;

/// <summary>
/// Entrega de um aviso interno a uma pessoa da equipe: notificação no sino (sempre) e e-mail (se a categoria não estiver
/// desligada nas preferências dela). O sino vem antes: se o e-mail falhar, a reentrega não duplica a notificação. Em
/// empresa não liberada, o e-mail só sai para o criador (RF-AUT-06, F9b); o sino não muda.
/// </summary>
public sealed class AvisoInterno
{
    private readonly INotificacoes _notificacoes;
    private readonly IPreferenciasNotificacao _preferencias;
    private readonly IEnviadorEmail _email;
    private readonly EnvioATerceiros _envio;

    public AvisoInterno(
        INotificacoes notificacoes, IPreferenciasNotificacao preferencias, IEnviadorEmail email, EnvioATerceiros envio)
    {
        ArgumentNullException.ThrowIfNull(envio);
        _envio = envio;
        ArgumentNullException.ThrowIfNull(notificacoes);
        ArgumentNullException.ThrowIfNull(preferencias);
        ArgumentNullException.ThrowIfNull(email);
        _notificacoes = notificacoes;
        _preferencias = preferencias;
        _email = email;
    }

    /// <param name="chave">Identifica o aviso para a idempotência do sino (ex.: <c>solicitacao-aberta:{id}</c>).</param>
    /// <param name="titulo">Texto curto do sino.</param>
    public async Task AvisarAsync(
        PessoaDaEquipe pessoa, CategoriaAviso categoria, string chave, string titulo, Uri? link, MensagemEmail email,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pessoa);
        ArgumentNullException.ThrowIfNull(email);
        await _notificacoes.RegistrarAsync(pessoa.UsuarioId, categoria, chave, titulo, link, cancellationToken);
        if (!(await _preferencias.EmailDesligadoAsync(pessoa.UsuarioId, cancellationToken)).Contains(categoria)
            && await _envio.PodeAvisarAsync(email.Categoria, pessoa.UsuarioId, cancellationToken))
        {
            await _email.EnviarAsync(email, cancellationToken);
        }
    }
}
