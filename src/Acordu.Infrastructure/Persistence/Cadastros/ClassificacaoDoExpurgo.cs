using Acordu.Domain.Entities;
using Acordu.Infrastructure.Persistence.Agenda;
using Acordu.Infrastructure.Persistence.Atividades;
using Acordu.Infrastructure.Persistence.Auditoria;
using Acordu.Infrastructure.Persistence.Colaboracao;
using Acordu.Infrastructure.Persistence.Notificacoes;
using Acordu.Infrastructure.Persistence.Outbox;
using Acordu.Infrastructure.Persistence.Protocolo;
using Acordu.Infrastructure.Seguranca;

namespace Acordu.Infrastructure.Persistence.Cadastros;

/// <summary>
/// Classificação de toda entidade com <c>TenantId</c> para o expurgo de cadastro abandonado (RF-AUT-07; spec do F9,
/// decisão 3). Um teste confere que toda entidade do modelo está em exatamente uma das listas: entidade nova esquecida
/// não pode fazer o expurgo apagar dado real.
/// </summary>
public static class ClassificacaoDoExpurgo
{
    /// <summary>Registro de negócio: um só impede o expurgo.</summary>
    public static readonly IReadOnlySet<Type> Dados = new HashSet<Type>
    {
        typeof(AcessoPortal),
        typeof(AjusteSaldo),
        typeof(Apontamento),
        typeof(Categoria),
        typeof(Cliente),
        typeof(ComentarioTarefa),
        typeof(Contrato),
        typeof(ConversaPorEmail),
        typeof(ConviteEquipe),
        typeof(ConvitePortal),
        typeof(Cronometro),
        typeof(EmailRecebido),
        typeof(Interacao),
        typeof(ItemTriagemEmail),
        typeof(Projeto),
        typeof(RecorrenciaTarefa),
        typeof(Solicitacao),
        typeof(Tarefa),
    };

    /// <summary>
    /// O que o registro cria (papéis padrão, calendário, o vínculo do criador; configuração editada nele não conta) e os
    /// rastros. Apagados junto com a empresa. Vínculo de outra pessoa conta como dado (confere o expurgo).
    /// </summary>
    public static readonly IReadOnlySet<Type> RegistroOuRastro = new HashSet<Type>
    {
        typeof(CalendarioUtil),
        typeof(Papel),
        typeof(Vinculo),
        typeof(AssinaturaAgendaRegistro),
        typeof(Atividade),
        typeof(RegistroAuditoria),
        typeof(Seguidor),
        typeof(MarcadorAviso),
        typeof(Notificacao),
        typeof(PreferenciaNotificacao),
        typeof(MensagemOutbox),
        typeof(Contador),
        typeof(RefreshToken),
    };
}
