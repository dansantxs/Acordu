namespace Acordu.Domain.Enums;

/// <summary>Ciclo interno da solicitação (RF-SOL-03). FECHADA e CANCELADA são finais.</summary>
public enum StatusSolicitacao
{
    Aberta = 1,
    EmAndamento = 2,
    AguardandoCliente = 3,
    AguardandoTerceiro = 4,
    Resolvida = 5,
    Fechada = 6,
    Cancelada = 7,
}
