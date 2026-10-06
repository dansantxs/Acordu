namespace Acordu.Domain.Acesso;

/// <summary>
/// Catálogo de permissões do v1 (segurança §3.1). Papel é conjunto destas chaves; a checagem por caso de uso é da
/// Application. A divisão vale pelo conjunto, não pelo prefixo: <c>portal.gerenciar_acessos</c> é interna.
/// </summary>
public static class Permissoes
{
    public const string ClienteVer = "cliente.ver";
    public const string ClienteGerenciar = "cliente.gerenciar";
    public const string ContratoVer = "contrato.ver";
    public const string ContratoGerenciar = "contrato.gerenciar";
    public const string ContratoAjustarSaldo = "contrato.ajustar_saldo";
    public const string ContratoFecharPeriodo = "contrato.fechar_periodo";
    public const string SolicitacaoVer = "solicitacao.ver";
    public const string SolicitacaoCriar = "solicitacao.criar";
    public const string SolicitacaoEditar = "solicitacao.editar";
    public const string SolicitacaoComentar = "solicitacao.comentar";
    public const string TarefaVer = "tarefa.ver";
    public const string TarefaCriar = "tarefa.criar";
    public const string TarefaEditar = "tarefa.editar";
    public const string ProjetoVer = "projeto.ver";
    public const string ProjetoGerenciar = "projeto.gerenciar";
    public const string ApontamentoVer = "apontamento.ver";
    public const string ApontamentoCriar = "apontamento.criar";
    public const string ApontamentoEditarTodos = "apontamento.editar_todos";
    public const string RecorrenciaVer = "recorrencia.ver";
    public const string RecorrenciaGerenciar = "recorrencia.gerenciar";
    public const string DashboardVer = "dashboard.ver";
    public const string ConfiguracaoGerenciar = "configuracao.gerenciar";
    public const string PortalGerenciarAcessos = "portal.gerenciar_acessos";
    public const string UsuarioConvidar = "usuario.convidar";
    public const string UsuarioRemover = "usuario.remover";
    public const string PapelGerenciar = "papel.gerenciar";
    public const string AuditoriaVer = "auditoria.ver";

    public const string PortalSolicitacaoVer = "portal.solicitacao.ver";
    public const string PortalSolicitacaoCriar = "portal.solicitacao.criar";
    public const string PortalSolicitacaoComentar = "portal.solicitacao.comentar";
    public const string PortalSolicitacaoAprovar = "portal.solicitacao.aprovar";
    public const string PortalExtratoVer = "portal.extrato.ver";

    /// <summary>Permissões da equipe do prestador (papel INTERNO).</summary>
    public static IReadOnlySet<string> Internas { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        ClienteVer, ClienteGerenciar,
        ContratoVer, ContratoGerenciar, ContratoAjustarSaldo, ContratoFecharPeriodo,
        SolicitacaoVer, SolicitacaoCriar, SolicitacaoEditar, SolicitacaoComentar,
        TarefaVer, TarefaCriar, TarefaEditar,
        ProjetoVer, ProjetoGerenciar,
        ApontamentoVer, ApontamentoCriar, ApontamentoEditarTodos,
        RecorrenciaVer, RecorrenciaGerenciar,
        DashboardVer,
        ConfiguracaoGerenciar,
        PortalGerenciarAcessos,
        UsuarioConvidar, UsuarioRemover,
        PapelGerenciar,
        AuditoriaVer,
    };

    /// <summary>Permissões do portal (só em papel CLIENTE).</summary>
    public static IReadOnlySet<string> DoPortal { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        PortalSolicitacaoVer, PortalSolicitacaoCriar, PortalSolicitacaoComentar, PortalSolicitacaoAprovar, PortalExtratoVer,
    };

    /// <summary>Permissões que um papel do tipo dado pode conter.</summary>
    public static IReadOnlySet<string> Permitidas(Enums.TipoPapel tipo) => tipo switch
    {
        Enums.TipoPapel.Interno => Internas,
        Enums.TipoPapel.Cliente => DoPortal,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de papel inválido."),
    };
}
