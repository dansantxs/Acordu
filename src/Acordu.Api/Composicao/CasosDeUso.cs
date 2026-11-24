using Acordu.Application.Abstractions.Eventos;
using Acordu.Application.Agenda;
using Acordu.Application.Anexos;
using Acordu.Application.Apontamentos;
using Acordu.Application.Atividades;
using Acordu.Application.Auditoria;
using Acordu.Application.Capacidade;
using Acordu.Application.Categorias;
using Acordu.Application.Clientes;
using Acordu.Application.Colaboracao;
using Acordu.Application.Common;
using Acordu.Application.Configuracoes;
using Acordu.Application.Contratos;
using Acordu.Application.Dashboard;
using Acordu.Application.Empresas;
using Acordu.Application.Equipe;
using Acordu.Application.Exemplos;
using Acordu.Application.ListaEspera;
using Acordu.Application.Nebrys;
using Acordu.Application.Notificacoes;
using Acordu.Application.Papeis;
using Acordu.Application.Portal;
using Acordu.Application.AcessosPortal;
using Acordu.Application.Projetos;
using Acordu.Application.Recorrencias;
using Acordu.Application.Solicitacoes;
using Acordu.Application.Sugestoes;
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
        services.AddScoped<ComandosAssinaturaAgenda>();
        services.AddScoped<FeedAgenda>();
        services.AddScoped<AvisoInterno>();
        services.AddScoped<ValidacaoMencoes>();
        services.AddScoped<ComandosNotificacoes>();
        services.AddScoped<ComandosSugestao>();
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
        // Dados de exemplo (F14r): a carga do conjunto fictício e as ações do Dono.
        services.AddScoped<CargaDosDadosFicticios>();
        services.AddScoped<ComandosExemplos>();
        services.AddScoped<ComandosAcessoPortal>();
        services.AddScoped<RevogacaoAcessoPortal>();
        services.AddScoped<EnvioATerceiros>();
        services.AddScoped<ComandosLinkPortal>();
        services.AddScoped<ComandosEquipe>();
        services.AddScoped<ComandosComentarioTarefa>();
        services.AddScoped<LeituraArquivosCliente>();
        services.AddScoped<LeituraCapacidade>();
        services.AddScoped<ComandosPapel>();
        services.AddScoped<FechamentoAutomatico>();
        services.AddScoped<VencimentoTestes>();
        // Área da Nebrys (F14l): ações sobre a situação das empresas e liberação de documento.
        services.AddScoped<AcoesEmpresasNebrys>();
        services.AddScoped<LiberacaoDocumentos>();
        // Convites de empresa (F14n): emitir, reenviar e revogar pela área.
        services.AddScoped<ConvitesEmpresaNebrys>();
        // Lista de espera (F14o): a inscrição pública e as ações da área (convidar a partir da entrada, descartar).
        services.AddScoped<InscricaoListaEspera>();
        services.AddScoped<ListaEsperaNebrys>();
        // Empresa de demonstração (F14s): criar, restaurar e indicar pela área; os dados fictícios inteiros.
        services.AddScoped<DemonstracaoNebrys>();
        services.AddScoped<SemeaduraDaDemonstracao>();
        services.AddScoped<ComandosPortal>();
        services.AddScoped<TarefasAbertas>();
        services.AddScoped<LeituraPortal>();

        // Reações de estado na mesma transação (modelo §7).
        services.AddScoped<IManipuladorEvento<InteracaoRegistrada>, PrimeiraRespostaERetomada>();
        services.AddScoped<AndamentoPelaTarefa>();
        services.AddScoped<IManipuladorEvento<TarefaCriada>>(sp => sp.GetRequiredService<AndamentoPelaTarefa>());
        services.AddScoped<IManipuladorEvento<TarefaStatusAlterada>>(sp => sp.GetRequiredService<AndamentoPelaTarefa>());
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
        services.AddScoped<IManipuladorPosCommit<PessoasMencionadas>, AvisosDeMencao>();

        // Seguidores (RF-COL-03): seguimento automático e avisos SEGUIDAS.
        services.AddScoped<ComandosSeguimento>();
        services.AddScoped<SeguimentoAutomatico>();
        services.AddScoped<IManipuladorPosCommit<SolicitacaoAberta>>(sp => sp.GetRequiredService<SeguimentoAutomatico>());
        services.AddScoped<IManipuladorPosCommit<SolicitacaoAtribuida>>(sp => sp.GetRequiredService<SeguimentoAutomatico>());
        services.AddScoped<IManipuladorPosCommit<InteracaoRegistrada>>(sp => sp.GetRequiredService<SeguimentoAutomatico>());
        services.AddScoped<IManipuladorPosCommit<PessoasMencionadas>>(sp => sp.GetRequiredService<SeguimentoAutomatico>());
        services.AddScoped<IManipuladorPosCommit<TarefaCriada>>(sp => sp.GetRequiredService<SeguimentoAutomatico>());
        services.AddScoped<IManipuladorPosCommit<TarefaAtribuida>>(sp => sp.GetRequiredService<SeguimentoAutomatico>());
        services.AddScoped<IManipuladorPosCommit<ComentarioTarefaRegistrado>>(sp => sp.GetRequiredService<SeguimentoAutomatico>());
        services.AddScoped<AvisosAosSeguidores>();
        services.AddScoped<IManipuladorPosCommit<InteracaoRegistrada>>(sp => sp.GetRequiredService<AvisosAosSeguidores>());
        services.AddScoped<IManipuladorPosCommit<ComentarioTarefaRegistrado>>(sp => sp.GetRequiredService<AvisosAosSeguidores>());
        services.AddScoped<IManipuladorPosCommit<SolicitacaoStatusAlterado>>(sp => sp.GetRequiredService<AvisosAosSeguidores>());
        services.AddScoped<IManipuladorPosCommit<TarefaStatusAlterada>>(sp => sp.GetRequiredService<AvisosAosSeguidores>());

        // Feed de atividade (RF-COL-05).
        services.AddScoped<LeituraAtividades>();
        services.AddScoped<RegistroDeAtividades>();
        services.AddScoped<IManipuladorPosCommit<SolicitacaoAberta>>(sp => sp.GetRequiredService<RegistroDeAtividades>());
        services.AddScoped<IManipuladorPosCommit<SolicitacaoAtribuida>>(sp => sp.GetRequiredService<RegistroDeAtividades>());
        services.AddScoped<IManipuladorPosCommit<SolicitacaoStatusAlterado>>(sp => sp.GetRequiredService<RegistroDeAtividades>());
        services.AddScoped<IManipuladorPosCommit<InteracaoRegistrada>>(sp => sp.GetRequiredService<RegistroDeAtividades>());
        services.AddScoped<IManipuladorPosCommit<TarefaCriada>>(sp => sp.GetRequiredService<RegistroDeAtividades>());
        services.AddScoped<IManipuladorPosCommit<TarefaAtribuida>>(sp => sp.GetRequiredService<RegistroDeAtividades>());
        services.AddScoped<IManipuladorPosCommit<TarefaStatusAlterada>>(sp => sp.GetRequiredService<RegistroDeAtividades>());
        services.AddScoped<IManipuladorPosCommit<TarefaProjetoAlterado>>(sp => sp.GetRequiredService<RegistroDeAtividades>());
        services.AddScoped<IManipuladorPosCommit<ComentarioTarefaRegistrado>>(sp => sp.GetRequiredService<RegistroDeAtividades>());
        services.AddScoped<AlertasSla>();
        services.AddScoped<AvisosCronometroEsquecido>();
        services.AddScoped<LeituraCronometro>();

        // Alertas de consumo de contrato (RF-CTR-07).
        services.AddScoped<AlertasConsumo>();
        services.AddScoped<IManipuladorPosCommit<ApontamentoRegistrado>>(sp => sp.GetRequiredService<AlertasConsumo>());
        services.AddScoped<IManipuladorPosCommit<ApontamentoAlterado>>(sp => sp.GetRequiredService<AlertasConsumo>());
        services.AddScoped<IManipuladorPosCommit<ApontamentoExcluido>>(sp => sp.GetRequiredService<AlertasConsumo>());
        services.AddScoped<IManipuladorPosCommit<AjusteSaldoRegistrado>>(sp => sp.GetRequiredService<AlertasConsumo>());
        return services;
    }
}
