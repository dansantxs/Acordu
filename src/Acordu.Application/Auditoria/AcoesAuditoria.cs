namespace Acordu.Application.Auditoria;

/// <summary>Nomes das ações auditadas (segurança §8), no formato <c>área.ação</c>, estáveis: a tela filtra por eles.</summary>
public static class AcoesAuditoria
{
    public const string LoginSucesso = "login.sucesso";

    /// <summary>Sem tenant (o login acontece antes de escolher um): fica só para a operação.</summary>
    public const string LoginFalha = "login.falha";

    public const string LinkPortalTroca = "portal.link.troca";
}
