namespace Acordu.Application.Auditoria;

/// <summary>Nomes das ações auditadas (segurança §8), no formato <c>área.ação</c>, estáveis: a tela filtra por eles.</summary>
public static class AcoesAuditoria
{
    public const string LoginSucesso = "login.sucesso";

    /// <summary>Sem tenant (o login acontece antes de escolher um): fica só para a operação.</summary>
    public const string LoginFalha = "login.falha";

    public const string LinkPortalTroca = "portal.link.troca";

    public const string EquipeConvite = "equipe.convite";
    public const string EquipeConviteRevogacao = "equipe.convite.revogacao";
    public const string EquipePapelTroca = "equipe.papel.troca";
    public const string EquipeRemocao = "equipe.remocao";

    public const string PortalAcessoConcessao = "portal.acesso.concessao";
    public const string PortalAcessoEscopo = "portal.acesso.escopo";
    public const string PortalAcessoRevogacao = "portal.acesso.revogacao";
    public const string PortalLinkEmissao = "portal.link.emissao";
    public const string PortalLinkRevogacao = "portal.link.revogacao";

    public const string ContratoCriacao = "contrato.criacao";
    public const string ContratoEdicao = "contrato.edicao";
    public const string ContratoSuspensao = "contrato.suspensao";
    public const string ContratoReativacao = "contrato.reativacao";
    public const string ContratoEncerramento = "contrato.encerramento";
    public const string ContratoAjusteSaldo = "contrato.ajuste_saldo";
    public const string ContratoFechamentoPeriodo = "contrato.fechamento_periodo";

    public const string ApontamentoEdicao = "apontamento.edicao";
    public const string ApontamentoExclusao = "apontamento.exclusao";

    public const string SolicitacaoStatus = "solicitacao.status";
    public const string SolicitacaoResponsavel = "solicitacao.responsavel";
}
