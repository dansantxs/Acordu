namespace Acordu.Domain.Enums;

/// <summary>Status que o cliente vê no portal (RF-POR-02); função pura do status interno.</summary>
public enum StatusExterno
{
    Recebida = 1,
    EmAndamento = 2,
    AguardandoVoce = 3,
    Concluida = 4,
    Cancelada = 5,
}
