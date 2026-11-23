using Acordu.Application.Empresas;

namespace Acordu.Application.Cadastro;

/// <summary>Recusas do cadastro da empresa que são erro de campo (<c>400</c>, contrato §2; design do F14, §1.2).</summary>
public static class MensagensDeCadastro
{
    /// <summary>Campo do documento no corpo do <c>POST /auth/registro</c>.</summary>
    public const string CampoDocumento = "documento";

    public const string DocumentoObrigatorio = "Informe o CPF ou CNPJ.";

    /// <summary>Campo do token do convite de empresa no corpo do <c>POST /auth/registro</c> (F14n, design D11).</summary>
    public const string CampoConvite = "convite";

    /// <summary>Campo do e-mail no corpo do <c>POST /auth/registro</c>.</summary>
    public const string CampoEmail = "email";

    /// <summary>O convite é de outro e-mail (F14-49, decisão 7 do G2): o link não passa adiante. Não diz qual é o e-mail.</summary>
    public const string EmailDiferenteDoConvite = "Use o e-mail que recebeu o convite: o cadastro por convite vale só para ele.";

    /// <summary>Modo convite sem convite (F14-45): o cadastro está fechado; a orientação é pedir um à Nebrys.</summary>
    public static string SoPorConvite(OpcoesNebrys nebrys)
    {
        ArgumentNullException.ThrowIfNull(nebrys);
        return $"No momento, o cadastro no Acordu é só por convite. Para pedir um, {nebrys.FaleComANebrys}.";
    }

    /// <summary>
    /// Convite inexistente, vencido, já usado ou revogado (F14-45, F14-48), sem distinguir o motivo, como os outros convites
    /// (segurança §4).
    /// </summary>
    public static string ConviteInvalido(OpcoesNebrys nebrys)
    {
        ArgumentNullException.ThrowIfNull(nebrys);
        return $"Este convite não vale mais: venceu, já foi usado ou foi revogado. Para pedir outro, {nebrys.FaleComANebrys}.";
    }

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
