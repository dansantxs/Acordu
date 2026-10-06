using Acordu.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Acordu.Infrastructure.Email;

/// <summary>
/// Envio de e-mail só em log, até a integração com o Resend. Por padrão registra apenas a categoria (nada de dado de
/// cliente em log, segurança §6); <c>Email__RegistrarConteudo=true</c> (só em desenvolvimento) inclui destinatário,
/// assunto e corpo, para seguir os links dos e-mails localmente.
/// </summary>
public sealed partial class EnviadorEmailLog : IEnviadorEmail
{
    private readonly ILogger<EnviadorEmailLog> _logger;
    private readonly bool _registrarConteudo;

    public EnviadorEmailLog(ILogger<EnviadorEmailLog> logger, bool registrarConteudo)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _registrarConteudo = registrarConteudo;
    }

    public Task EnviarAsync(MensagemEmail mensagem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        if (_registrarConteudo)
            EmailCompleto(_logger, mensagem.Categoria, mensagem.Para, mensagem.Assunto, mensagem.Texto);
        else
            EmailResumido(_logger, mensagem.Categoria);
        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 100, Level = LogLevel.Information, Message = "E-mail '{Categoria}' (envio desativado: só log).")]
    private static partial void EmailResumido(ILogger logger, string categoria);

    [LoggerMessage(EventId = 101, Level = LogLevel.Information,
        Message = "E-mail '{Categoria}' para {Para}: {Assunto}\n{Texto}")]
    private static partial void EmailCompleto(ILogger logger, string categoria, string para, string assunto, string texto);
}
