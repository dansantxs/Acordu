namespace Acordu.Domain.Enums;

/// <summary>Canal de entrada. EMAIL e WHATSAPP são do v2, previstos para não exigir migration de enum.</summary>
public enum OrigemSolicitacao
{
    Interna = 1,
    Portal = 2,
    Email = 3,
    Whatsapp = 4,
}
