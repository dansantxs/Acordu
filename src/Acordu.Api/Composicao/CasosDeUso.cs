using Acordu.Application.Abstractions.Eventos;
using Acordu.Application.Agenda;
using Acordu.Application.Anexos;
using Acordu.Application.Apontamentos;
using Acordu.Application.Auditoria;
using Acordu.Application.Categorias;
using Acordu.Application.Clientes;
using Acordu.Application.Common;
using Acordu.Application.Configuracoes;
using Acordu.Application.Contratos;
using Acordu.Application.Dashboard;
using Acordu.Application.Equipe;
using Acordu.Application.Notificacoes;
using Acordu.Application.Papeis;
using Acordu.Application.Portal;
using Acordu.Application.AcessosPortal;
using Acordu.Application.Projetos;
using Acordu.Application.Recorrencias;
using Acordu.Application.Solicitacoes;
using Acordu.Application.Tarefas;
using Acordu.Domain.Events;

namespace Acordu.Api.Composicao;

/// <summary>
/// Casos de uso da Application. Registrados aqui (composition root) para a Application seguir sem pacotes, só com o
/// Domain, e compilar isolada.
/// </summary>
internal static class CasosDeUso
{
    public static IServiceCollection AddCasosDeUso(this IServiceCollection services)
    {
        services.AddScoped<RelogioDoTenant>();
        services.AddScoped<RecebimentoAnexos>();
        services.AddScoped<DownloadAnexos>();
        services.AddScoped<LogotipoPortal>();
        services.AddScoped<LeituraDashboard>();
        services.AddScoped<LeituraAgenda>();
        services.AddScoped<AvisoInterno>();
        services.AddScoped<ComandosNotificacoes>();
        services.AddScoped<ComandosCliente>();
        services.AddScoped<ComandosContrato>();
        services.AddScoped<ComandosSolicitacao>();
        services.AddScoped<ComandosTarefa>();
        services.AddScoped<ComandosProjeto>();
        services.AddScoped<ComandosRecorrencia>();
        services.AddScoped<GeracaoRecorrencias>();
        services.AddScoped<ComandosApontamento>();
        services.AddScoped<ComandosConfiguracao>();
        services.AddScoped<ComandosCategoria>();
        services.AddScoped<ComandosAcessoPortal>();
        services.AddScoped<RevogacaoAcessoPortal>();
        services.AddScoped<ComandosLinkPortal>();
        services.AddScoped<ComandosEquipe>();
        services.AddScoped<ComandosPapel>();
        services.AddScoped<FechamentoAutomatico>();
        services.AddScoped<ComandosPortal>();
        services.AddScoped<LeituraPortal>();

        // Reações de estado na mesma transação (modelo §7).
        services.AddScoped<IManipuladorEvento<InteracaoRegistrada>, PrimeiraRespostaERetomada>();
        // Auditoria de status e responsável da solicitação, por qualquer caminho (ADR 0008).
        services.AddScoped<AuditoriaDeSolicitacoes>();
        services.AddScoped<IManipuladorEvento<SolicitacaoStatusAlterado>>(sp => sp.GetRequiredService<AuditoriaDeSolicitacoes>());
        services.AddScoped<IManipuladorEvento<SolicitacaoAtribuida>>(sp => sp.GetRequiredService<AuditoriaDeSolicitacoes>());
        services.AddScoped<IManipuladorEvento<SolicitacaoContratoAlterado>>(sp => sp.GetRequiredService<AuditoriaDeSolicitacoes>());

        // Efeitos externos depois do commit, pela outbox.
        services.AddScoped<IManipuladorPosCommit<ConvitePortalCriado>, EnvioConvitePortal>();
        services.AddScoped<IManipuladorPosCommit<ConviteEquipeCriado>, EnvioConviteEquipe>();

        // E-mails ao contato da solicitação (RF-NOT-01): uma classe, quatro eventos.
        services.AddScoped<AvisosAoContato>();
        services.AddScoped<IManipuladorPosCommit<SolicitacaoAberta>>(sp => sp.GetRequiredService<AvisosAoContato>());
        services.AddScoped<IManipuladorPosCommit<InteracaoRegistrada>>(sp => sp.GetRequiredService<AvisosAoContato>());
        services.AddScoped<IManipuladorPosCommit<SolicitacaoStatusAlterado>>(sp => sp.GetRequiredService<AvisosAoContato>());
        services.AddScoped<IManipuladorPosCommit<SolicitacaoResolvida>>(sp => sp.GetRequiredService<AvisosAoContato>());

        // E-mails internos (RF-NOT-02): nova solicitação e atribuição; SLA pelo job tick.
        services.AddScoped<AvisosAEquipe>();
        services.AddScoped<IManipuladorPosCommit<SolicitacaoAberta>>(sp => sp.GetRequiredService<AvisosAEquipe>());
        services.AddScoped<IManipuladorPosCommit<SolicitacaoAtribuida>>(sp => sp.GetRequiredService<AvisosAEquipe>());
        services.AddScoped<AlertasSla>();

        // Alertas de consumo de contrato (RF-CTR-07).
        services.AddScoped<AlertasConsumo>();
        services.AddScoped<IManipuladorPosCommit<ApontamentoRegistrado>>(sp => sp.GetRequiredService<AlertasConsumo>());
        services.AddScoped<IManipuladorPosCommit<ApontamentoAlterado>>(sp => sp.GetRequiredService<AlertasConsumo>());
        services.AddScoped<IManipuladorPosCommit<ApontamentoExcluido>>(sp => sp.GetRequiredService<AlertasConsumo>());
        services.AddScoped<IManipuladorPosCommit<AjusteSaldoRegistrado>>(sp => sp.GetRequiredService<AlertasConsumo>());
        return services;
    }
}
