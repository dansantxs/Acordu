namespace Acordu.Domain.Enums;

/// <summary>O que um contato com papel Cliente enxerga no portal (ADR 0002, segurança §3.2).</summary>
public enum EscopoPortal
{
    /// <summary>Só as solicitações que abriu ou em que é o contato; sem extrato.</summary>
    Proprias = 1,

    /// <summary>Todas as solicitações do cliente, com extrato.</summary>
    TodasDoCliente = 2,
}
