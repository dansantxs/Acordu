using Acordu.Application.Abstractions;

namespace Acordu.Application.Autenticacao;

/// <summary>Textos dos e-mails da conta (confirmação e redefinição de senha). Remetente: o Acordu.</summary>
public static class MensagensDeConta
{
    public static MensagemEmail Confirmacao(string email, string nome, Uri link, TimeSpan validade)
    {
        ArgumentNullException.ThrowIfNull(link);
        return new MensagemEmail(
            email,
            "Confirme seu e-mail no Acordu",
            $"""
            Olá, {nome}!

            Para confirmar este e-mail na sua conta do Acordu, acesse o link abaixo (válido por {validade.TotalHours:0} horas):
            {link}

            Se não foi você que criou a conta, pode ignorar este e-mail.
            """,
            "confirmacao-email");
    }

    public static MensagemEmail Redefinicao(string email, string nome, Uri link, TimeSpan validade)
    {
        ArgumentNullException.ThrowIfNull(link);
        return new MensagemEmail(
            email,
            "Redefinição de senha do Acordu",
            $"""
            Olá, {nome}!

            Recebemos um pedido para redefinir a senha da sua conta do Acordu. Para criar uma senha nova, acesse o link
            abaixo (válido por {validade.TotalMinutes:0} minutos):
            {link}

            Ao redefinir, todas as sessões abertas são encerradas. Se não foi você que pediu, ignore este e-mail: a senha
            atual continua valendo.
            """,
            "redefinicao-senha");
    }
}
