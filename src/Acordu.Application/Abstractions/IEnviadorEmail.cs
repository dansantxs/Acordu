namespace Acordu.Application.Abstractions;

/// <summary>
/// Envio de e-mail transacional. Chamado só de manipuladores pós-commit (outbox), nunca dentro da transação do caso de
/// uso (exceção: o link por solicitação, que volta na resposta). Produção: Resend por HTTP; sem chave configurada, só log.
/// Falha no envio lança exceção: a outbox tenta de novo.
/// </summary>
public interface IEnviadorEmail
{
    Task EnviarAsync(MensagemEmail mensagem, CancellationToken cancellationToken = default);
}

/// <summary>
/// E-mail em texto simples (a implementação deriva o HTML). <see cref="Categoria"/> identifica o tipo no log, sem dado do
/// destinatário. <see cref="NomeRemetente"/> é o nome exibido no "De" (o prestador); o endereço é sempre o do Acordu.
/// <see cref="ResponderPara"/> vira o Reply-To: a resposta do contato vai para a caixa do prestador (F5a).
/// </summary>
public sealed record MensagemEmail(
    string Para, string Assunto, string Texto, string Categoria, string? NomeRemetente = null, string? ResponderPara = null);
