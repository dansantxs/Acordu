namespace Acordu.Infrastructure.Email;

/// <summary>
/// Configuração do e-mail (ADR 0006: só variáveis de ambiente). Com <see cref="ChaveResend"/>, envia pelo Resend; sem
/// ela, só registra em log. <see cref="Remetente"/> é o endereço do domínio verificado (ex.: <c>nao-responda@send.acordu.app.br</c>).
/// </summary>
public sealed record OpcoesEmail(string? ChaveResend, string Remetente, string NomePadrao, bool RegistrarConteudo)
{
    public const string RemetentePadrao = "nao-responda@send.acordu.app.br";
    public const string NomeRemetentePadrao = "Acordu";

    public bool UsaResend => !string.IsNullOrWhiteSpace(ChaveResend);
}
