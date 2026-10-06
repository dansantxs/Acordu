namespace Acordu.Application.Abstractions;

/// <summary>
/// Envio de e-mail transacional. Chamado só de manipuladores pós-commit (outbox), nunca dentro da transação do caso de
/// uso. A implementação de produção (Resend) entra em PR próprio; até lá, o envio é registrado em log.
/// </summary>
public interface IEnviadorEmail
{
    Task EnviarAsync(MensagemEmail mensagem, CancellationToken cancellationToken = default);
}

/// <summary>E-mail em texto simples. <see cref="Categoria"/> identifica o tipo no log, sem dado do destinatário.</summary>
public sealed record MensagemEmail(string Para, string Assunto, string Texto, string Categoria);
