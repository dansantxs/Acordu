namespace Acordu.Application.Common;

/// <summary>
/// A empresa ainda não pode enviar e-mail a terceiros: quem a criou não confirmou o e-mail (RF-AUT-06, F9) → 422
/// <c>email-nao-confirmado</c>.
/// </summary>
public sealed class EmailNaoConfirmadoException : Exception
{
    public const string Mensagem =
        "Confirme o e-mail de quem criou a empresa para enviar convites e links. O link de confirmação está no e-mail do cadastro.";

    public EmailNaoConfirmadoException()
        : base(Mensagem)
    {
    }

    public EmailNaoConfirmadoException(string message)
        : base(message)
    {
    }

    public EmailNaoConfirmadoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
