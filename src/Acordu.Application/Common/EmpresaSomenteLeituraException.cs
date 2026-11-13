namespace Acordu.Application.Common;

/// <summary>
/// Gravação da empresa recusada porque ela está só leitura, VENCIDA ou CANCELADA (RF-EMP-02, ADR 0015) → 422
/// <c>empresa-somente-leitura</c>. Lançada pela guarda de gravação do contexto (Infrastructure), no fim do caso de uso,
/// depois da permissão, do 404 e das regras: nada é gravado.
/// </summary>
public sealed class EmpresaSomenteLeituraException : Exception
{
    public const string Mensagem =
        "A empresa está só leitura (o teste venceu ou a conta foi cancelada): dá para consultar tudo, mas não gravar. "
        + "Para voltar a usar, fale com a Nebrys.";

    public EmpresaSomenteLeituraException()
        : base(Mensagem)
    {
    }

    public EmpresaSomenteLeituraException(string message)
        : base(message)
    {
    }

    public EmpresaSomenteLeituraException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
