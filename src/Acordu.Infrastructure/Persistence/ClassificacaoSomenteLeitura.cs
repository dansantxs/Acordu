using Acordu.Domain.Entities;
using Acordu.Infrastructure.Persistence.Agenda;
using Acordu.Infrastructure.Persistence.Atividades;
using Acordu.Infrastructure.Persistence.Auditoria;
using Acordu.Infrastructure.Persistence.Colaboracao;
using Acordu.Infrastructure.Persistence.Notificacoes;
using Acordu.Infrastructure.Persistence.Outbox;
using Acordu.Infrastructure.Persistence.Protocolo;
using Acordu.Infrastructure.Seguranca;

namespace Acordu.Infrastructure.Persistence;

/// <summary>
/// Classificação de toda entidade com <c>TenantId</c> para o só leitura (RF-EMP-02, ADR 0015, item 2). A guarda do
/// <see cref="AcorduDbContext"/> recusa incluir, alterar ou remover o que é da empresa num tenant VENCIDO ou CANCELADO, e
/// deixa passar o que é da pessoa ou rastro. Tipo fora das duas listas é tratado como da empresa (nasce bloqueado), e um
/// teste de integração falha até ele ser classificado. Entidade filha (owned) segue a raiz.
/// </summary>
public static class ClassificacaoSomenteLeitura
{
    /// <summary>
    /// Dado da empresa: bloqueado no só leitura. O <see cref="Tenant"/> entra aqui: nome, portal, logotipo e SLA padrão
    /// são da empresa.
    /// </summary>
    public static readonly IReadOnlySet<Type> DaEmpresa = new HashSet<Type>
    {
        typeof(Tenant),
        typeof(AcessoPortal),
        typeof(AjusteSaldo),
        typeof(Apontamento),
        typeof(CalendarioUtil),
        typeof(Categoria),
        typeof(Cliente),
        typeof(ComentarioTarefa),
        typeof(Contador),
        typeof(Contrato),
        typeof(ConversaPorEmail),
        typeof(ConviteEquipe),
        typeof(ConvitePortal),
        typeof(Cronometro),
        typeof(EmailRecebido),
        typeof(Interacao),
        typeof(ItemTriagemEmail),
        typeof(MarcadorAviso),
        typeof(Papel),
        typeof(Projeto),
        typeof(RecorrenciaTarefa),
        typeof(Solicitacao),
        typeof(Tarefa),
        typeof(Vinculo),
    };

    /// <summary>
    /// Da pessoa ou rastro: passa no só leitura (decisão 10 do G2 do F14; F14-18, F14-100). Sino e preferências de aviso,
    /// link da agenda, seguidores e sessões são da pessoa; trilha de auditoria e feed de atividade são rastro do que já
    /// aconteceu. Na outbox, "rastro" só quer dizer que a guarda não bloqueia a tabela: o <c>ProcessadorOutbox</c> grava o
    /// estado das mensagens. Uma mensagem enfileirada fora de uma gravação da empresa passaria pela guarda; o descarte dos
    /// e-mails de empresa só leitura é do F14d (ADR 0015, item 5).
    /// </summary>
    public static readonly IReadOnlySet<Type> DaPessoaOuRastro = new HashSet<Type>
    {
        typeof(AssinaturaAgendaRegistro),
        typeof(Atividade),
        typeof(MensagemOutbox),
        typeof(Notificacao),
        typeof(PreferenciaNotificacao),
        typeof(RefreshToken),
        typeof(RegistroAuditoria),
        typeof(Seguidor),
    };
}
