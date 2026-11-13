namespace Acordu.Domain.Enums;

/// <summary>
/// Situação comercial da empresa (RF-EMP-01, modelo de domínio §4.11.3). VENCIDA e CANCELADA são só leitura (RF-EMP-02).
/// A empresa de demonstração é ATIVA e fica fora do ciclo (design do F14, D1).
/// </summary>
public enum SituacaoEmpresa
{
    /// <summary>Período de teste, até <c>Tenant.FimDoTeste</c>.</summary>
    Teste = 1,

    /// <summary>Ativada pela Nebrys; sem fim de teste, nunca vence.</summary>
    Ativa = 2,

    /// <summary>O teste acabou (job diário); só leitura até a ativação, a prorrogação ou o expurgo.</summary>
    Vencida = 3,

    /// <summary>Cancelada pela Nebrys; só leitura até a reativação ou o expurgo.</summary>
    Cancelada = 4,
}
