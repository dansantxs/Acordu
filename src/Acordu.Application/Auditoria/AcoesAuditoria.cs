namespace Acordu.Application.Auditoria;

/// <summary>Nomes das ações auditadas (segurança §8), no formato <c>área.ação</c>, estáveis: a tela filtra por eles.</summary>
public static class AcoesAuditoria
{
    public const string LoginSucesso = "login.sucesso";

    /// <summary>Sem tenant (o login acontece antes de escolher um): fica só para a operação.</summary>
    public const string LoginFalha = "login.falha";

    public const string LinkPortalTroca = "portal.link.troca";

    /// <summary>
    /// Sessão de operação da Nebrys aberta (ADR 0014, itens 2 e 6; F14k): sem tenant, com o operador em <c>usuario</c>.
    /// Fica só para a operação, como o login com falha.
    /// </summary>
    public const string NebrysSessao = "nebrys.sessao";

    /// <summary>Sessão de operação recusada: sem tenant, com o e-mail digitado e o motivo (a resposta não distingue).</summary>
    public const string NebrysSessaoFalha = "nebrys.sessao.falha";

    /// <summary>
    /// Ações da Nebrys sobre a situação de uma empresa (F14l): sem tenant, o operador em <c>usuario</c>, a empresa no alvo e
    /// a situação antes e depois em <c>dados</c> (ADR 0014, item 6; design D14).
    /// </summary>
    public const string NebrysEmpresaAtivacao = "nebrys.empresa.ativacao";

    public const string NebrysEmpresaProrrogacao = "nebrys.empresa.prorrogacao";
    public const string NebrysEmpresaCancelamento = "nebrys.empresa.cancelamento";
    public const string NebrysEmpresaReativacao = "nebrys.empresa.reativacao";

    /// <summary>
    /// Liberação de um documento para novo teste (F14-71, F14-108): sem tenant, sem alvo e sem o documento (nem o HMAC);
    /// <c>dados</c> só com as datas da liberação, antes e depois.
    /// </summary>
    public const string NebrysDocumentoLiberacao = "nebrys.documento.liberacao";

    /// <summary>
    /// Convites de empresa da Nebrys (F14-72 a F14-74): sem tenant, o operador em <c>usuario</c>, o convite no alvo e, em
    /// <c>dados</c>, o e-mail convidado e a situação e a validade antes e depois (nunca o token).
    /// </summary>
    public const string NebrysConviteEmissao = "nebrys.convite.emissao";

    public const string NebrysConviteReenvio = "nebrys.convite.reenvio";
    public const string NebrysConviteRevogacao = "nebrys.convite.revogacao";

    /// <summary>
    /// Lista de espera na área da Nebrys (F14-76, F14-77): sem tenant, o operador em <c>usuario</c>, a entrada no alvo e, em
    /// <c>dados</c>, o e-mail dela e a situação antes e depois (no convite, o convite emitido; no descarte, só o antes).
    /// </summary>
    public const string NebrysListaConvite = "nebrys.lista.convite";

    public const string NebrysListaDescarte = "nebrys.lista.descarte";

    public const string EquipeConvite = "equipe.convite";
    public const string EquipeConviteRevogacao = "equipe.convite.revogacao";
    public const string EquipePapelTroca = "equipe.papel.troca";
    public const string EquipeRemocao = "equipe.remocao";
    public const string EquipeCapacidade = "equipe.capacidade";

    public const string AgendaLinkEmissao = "agenda.link.emissao";
    public const string AgendaLinkRevogacao = "agenda.link.revogacao";

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
    public const string SolicitacaoContrato = "solicitacao.contrato";

    /// <summary>
    /// Dados de exemplo carregados ou apagados pelo Dono (RF-EMP-05, F14r): a empresa no alvo e, em <c>dados</c>, quantos
    /// clientes de exemplo entraram ou saíram.
    /// </summary>
    public const string ExemplosCarga = "exemplos.carga";

    public const string ExemplosRemocao = "exemplos.remocao";
}
