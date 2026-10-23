using Acordu.Domain.Entities;
using Acordu.Domain.Enums;

namespace Acordu.Application.Portal;

/// <summary>
/// Status externo como o portal mostra (RF-POR-02 com a aprovação da entrega): para quem pode aprovar, a solicitação
/// RESOLVIDA aparece como AGUARDANDO_VOCE, porque a entrega espera a aprovação dele. Sem a permissão, vale a função do
/// domínio (<see cref="Solicitacao.ParaExterno"/>), que segue CONCLUIDA. Lista, filtro e detalhe usam esta regra.
/// </summary>
public static class StatusNoPortal
{
    public static StatusExterno Para(StatusSolicitacao status, bool podeAprovar) =>
        podeAprovar && status == StatusSolicitacao.Resolvida ? StatusExterno.AguardandoVoce : Solicitacao.ParaExterno(status);
}
