namespace Acordu.Domain.Enums;

/// <summary>
/// Os e-mails de situação aos Donos (RF-EMP-04, decisão 3 do G2 do F14, modelo §4.11.3): antes do fim do teste, no
/// vencimento e antes do expurgo. Quem decide qual é devido é <c>Tenant.AvisoDeSituacaoDevido</c>.
/// </summary>
public enum TipoAvisoSituacao
{
    /// <summary>Empresa em TESTE com o fim do teste perto (7 e 1 dia antes, pela configuração).</summary>
    AntesDoFimDoTeste = 1,

    /// <summary>Empresa que venceu: ficou só leitura, com a data do expurgo.</summary>
    Vencimento = 2,

    /// <summary>Empresa VENCIDA ou CANCELADA com o expurgo perto (7 dias antes, pela configuração).</summary>
    AntesDoExpurgo = 3,
}
