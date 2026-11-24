namespace Acordu.Application.Common;

/// <summary>
/// A empresa de demonstração não manda e-mail para fora do Acordu (RF-NEB-02, F14-89; design do F14, D13): convite da
/// equipe, convite do portal e link de acesso enviado por e-mail → 422 <c>demonstracao-sem-envio</c>, sem gravar nada.
/// </summary>
public sealed class DemonstracaoSemEnvioException : Exception
{
    public const string Mensagem =
        "Esta é a empresa de demonstração: nada sai dela por e-mail. Para mostrar o portal, emita o link sem enviar e copie-o.";

    public DemonstracaoSemEnvioException()
        : base(Mensagem)
    {
    }

    public DemonstracaoSemEnvioException(string message)
        : base(message)
    {
    }

    public DemonstracaoSemEnvioException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
