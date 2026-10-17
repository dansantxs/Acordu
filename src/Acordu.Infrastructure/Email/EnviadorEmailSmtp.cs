using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Acordu.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Acordu.Infrastructure.Email;

/// <summary>
/// Entrega por SMTP sem autenticação nem TLS, para o Mailpit do desenvolvimento (decidido em 16/10): os e-mails aparecem
/// em http://localhost:8025, com os links clicáveis, e nada sai para a internet. Em produção o envio é pelo Resend; este
/// enviador não serve para um servidor SMTP de verdade.
/// </summary>
public sealed partial class EnviadorEmailSmtp : IEnviadorEmail
{
    private readonly OpcoesEmail _opcoes;
    private readonly ILogger<EnviadorEmailSmtp> _logger;

    public EnviadorEmailSmtp(OpcoesEmail opcoes, ILogger<EnviadorEmailSmtp> logger)
    {
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(logger);
        if (!opcoes.UsaSmtp)
            throw new ArgumentException("Servidor SMTP não configurado.", nameof(opcoes));
        _opcoes = opcoes;
        _logger = logger;
    }

    public async Task EnviarAsync(MensagemEmail mensagem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        using var email = new MailMessage
        {
            From = new MailAddress(_opcoes.Remetente, FormatoEmail.NomeRemetente(mensagem.NomeRemetente, _opcoes), Encoding.UTF8),
            Subject = mensagem.Assunto,
            SubjectEncoding = Encoding.UTF8,
            Body = mensagem.Texto,
            BodyEncoding = Encoding.UTF8,
        };
        email.To.Add(mensagem.Para);
        email.AlternateViews.Add(
            AlternateView.CreateAlternateViewFromString(FormatoEmail.Html(mensagem.Texto), Encoding.UTF8, MediaTypeNames.Text.Html));

        using var smtp = new SmtpClient(_opcoes.SmtpHost, _opcoes.SmtpPorta) { EnableSsl = false };
        await smtp.SendMailAsync(email, cancellationToken);
        Enviado(_logger, mensagem.Categoria, _opcoes.SmtpHost!, _opcoes.SmtpPorta);
    }

    [LoggerMessage(EventId = 102, Level = LogLevel.Information, Message = "E-mail '{Categoria}' entregue por SMTP em {Host}:{Porta}.")]
    private static partial void Enviado(ILogger logger, string categoria, string host, int porta);
}
