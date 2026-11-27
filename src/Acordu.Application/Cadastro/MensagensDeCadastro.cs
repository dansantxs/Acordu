using Acordu.Application.Common;
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

    /// <summary>
    /// E-mail de domínio descartável (RF-AUT-10, F14-60; design D9), no cadastro e na lista de espera: <c>400</c> no campo do
    /// e-mail, com o mesmo texto nos dois.
    /// </summary>
    public const string EmailDescartavel =
        "Use um e-mail que você vai continuar usando: endereços de e-mail temporário (descartável) não são aceitos.";

    /// <summary>
    /// Recusa o e-mail de domínio descartável com <c>400</c> no <paramref name="campo"/>, antes de gravar qualquer coisa
    /// (F14-60). O e-mail já vem com o formato conferido.
    /// </summary>
    public static void RecusarDescartavel(IDominiosDescartaveis dominios, string email, string campo)
    {
        ArgumentNullException.ThrowIfNull(dominios);
        if (dominios.EhDescartavel(email))
            throw ErroDeCampo.Criar(campo, EmailDescartavel);
    }

    /// <summary>O convite é de outro e-mail (F14-49, decisão 7 do G2): o link não passa adiante. Não diz qual é o e-mail.</summary>
    public const string EmailDiferenteDoConvite = "Use o e-mail que recebeu o convite: o cadastro por convite vale só para ele.";

    /// <summary>Modo convite sem convite (F14-45): o cadastro está fechado; a orientação é pedir um à Nebrys.</summary>
    public static string SoPorConvite(OpcoesFornecedor fornecedor)
    {
        ArgumentNullException.ThrowIfNull(fornecedor);
        return $"No momento, o cadastro no Acordu é só por convite. Para pedir um, {fornecedor.FaleComOFornecedor}.";
    }

    /// <summary>
    /// Convite inexistente, vencido, já usado ou revogado (F14-45, F14-48), sem distinguir o motivo, como os outros convites
    /// (segurança §4).
    /// </summary>
    public static string ConviteInvalido(OpcoesFornecedor fornecedor)
    {
        ArgumentNullException.ThrowIfNull(fornecedor);
        return $"Este convite não vale mais: venceu, já foi usado ou foi revogado. Para pedir outro, {fornecedor.FaleComOFornecedor}.";
    }

    /// <summary>
    /// O documento já teve teste (F14-57, F14-58), sem liberação disponível: a orientação é falar com a Nebrys pelo canal da
    /// decisão 14 do G2. Não diz quando nem em que empresa (o registro não sabe).
    /// </summary>
    public static string DocumentoJaTeveTeste(OpcoesFornecedor fornecedor)
    {
        ArgumentNullException.ThrowIfNull(fornecedor);
        return $"Este CPF ou CNPJ já teve um período de teste no Acordu. Para testar de novo, {fornecedor.FaleComOFornecedor}.";
    }
}
