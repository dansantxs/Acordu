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

    public static string Status(StatusSolicitacao status) => status switch
    {
        StatusSolicitacao.Aberta => "aberta",
        StatusSolicitacao.EmAndamento => "em andamento",
        StatusSolicitacao.AguardandoCliente => "aguardando o cliente",
        StatusSolicitacao.AguardandoTerceiro => "aguardando terceiro",
        StatusSolicitacao.Resolvida => "resolvida",
        StatusSolicitacao.Fechada => "fechada",
        StatusSolicitacao.Cancelada => "cancelada",
        _ => status.ToString(),
    };

    public static string Status(StatusTarefa status) => status switch
    {
        StatusTarefa.AFazer => "a fazer",
        StatusTarefa.Fazendo => "em andamento",
        StatusTarefa.Concluida => "concluída",
        StatusTarefa.Cancelada => "cancelada",
        _ => status.ToString(),
    };
}
