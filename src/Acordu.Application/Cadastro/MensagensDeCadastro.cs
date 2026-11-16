using Acordu.Application.Empresas;

namespace Acordu.Application.Cadastro;

/// <summary>Recusas do cadastro da empresa que são erro de campo (<c>400</c>, contrato §2; design do F14, §1.2).</summary>
public static class MensagensDeCadastro
{
    /// <summary>Campo do documento no corpo do <c>POST /auth/registro</c>.</summary>
    public const string CampoDocumento = "documento";

    public const string DocumentoObrigatorio = "Informe o CPF ou CNPJ.";

    /// <summary>
    /// O documento já teve teste (F14-57, F14-58), sem liberação disponível: a orientação é falar com a Nebrys pelo canal da
    /// decisão 14 do G2. Não diz quando nem em que empresa (o registro não sabe).
    /// </summary>
    public static string DocumentoJaTeveTeste(OpcoesNebrys nebrys)
    {
        ArgumentNullException.ThrowIfNull(nebrys);
        return $"Este CPF ou CNPJ já teve um período de teste no Acordu. Para testar de novo, {nebrys.FaleComANebrys}.";
    }
}
