using Acordu.Domain.Enums;

namespace Acordu.Application.Notificacoes;

/// <summary>Rótulos em português para os e-mails.</summary>
internal static class Textos
{
    public static string Prioridade(Prioridade prioridade) => prioridade switch
    {
        Domain.Enums.Prioridade.Urgente => "urgente",
        Domain.Enums.Prioridade.Alta => "alta",
        Domain.Enums.Prioridade.Media => "média",
        Domain.Enums.Prioridade.Baixa => "baixa",
        _ => prioridade.ToString(),
    };
}
