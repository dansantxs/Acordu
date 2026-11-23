namespace Acordu.Domain.Enums;

/// <summary>
/// Situação de um convite de empresa (RF-AUT-08, F14n; modelo de domínio §4.14), calculada pelas datas do
/// <c>ConviteEmpresa</c> e o instante da leitura: não é gravada.
/// </summary>
public enum SituacaoConviteEmpresa
{
    /// <summary>Emitido, ainda não usado nem revogado, dentro da validade: o link abre o cadastro.</summary>
    Pendente = 1,

    /// <summary>Não usado nem revogado, mas a validade passou: o link não vale; a Nebrys pode reenviar.</summary>
    Vencido = 2,

    /// <summary>Um cadastro usou o convite (F14-47): não vale para outro (F14-48).</summary>
    Usado = 3,

    /// <summary>A Nebrys revogou (F14-74): o link não vale.</summary>
    Revogado = 4,
}
