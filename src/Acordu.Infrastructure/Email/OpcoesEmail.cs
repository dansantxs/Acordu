namespace Acordu.Infrastructure.Email;

/// <summary>
/// Configuração do e-mail (ADR 0006: só variáveis de ambiente). Com <see cref="ChaveResend"/>, envia pelo Resend; sem
/// ela e com <see cref="SmtpHost"/>, entrega por SMTP (no desenvolvimento, ao Mailpit do docker-compose.dev); sem
/// nenhum dos dois, só registra em log. <see cref="Remetente"/> é o endereço do domínio verificado
/// (ex.: <c>nao-responda@send.acordu.app.br</c>).
/// </summary>
public sealed record OpcoesEmail(
    string? ChaveResend, string Remetente, string NomePadrao, bool RegistrarConteudo, string? SmtpHost = null, int SmtpPorta = 1025)
{
    public const string RemetentePadrao = "nao-responda@send.acordu.app.br";
    public const string NomeRemetentePadrao = "Acordu";

    public bool UsaResend => !string.IsNullOrWhiteSpace(ChaveResend);

    public bool UsaSmtp => !UsaResend && !string.IsNullOrWhiteSpace(SmtpHost);
}
