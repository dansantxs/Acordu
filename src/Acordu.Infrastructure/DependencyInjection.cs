using Acordu.Application.Abstractions;
using Acordu.Application.Abstractions.Eventos;
using Acordu.Application.Agenda;
using Acordu.Application.Apontamentos;
using Acordu.Application.Atividades;
using Acordu.Application.Auditoria;
using Acordu.Application.Autenticacao;
using Acordu.Application.Capacidade;
using Acordu.Application.Categorias;
using Acordu.Application.Clientes;
using Acordu.Application.Contratos;
using Acordu.Application.Dashboard;
using Acordu.Application.Equipe;
using Acordu.Application.Notificacoes;
using Acordu.Application.Papeis;
using Acordu.Application.AcessosPortal;
using Acordu.Application.Portal;
using Acordu.Application.Portal.Extrato;
using Acordu.Application.Projetos;
using Acordu.Application.Recorrencias;
using Acordu.Application.Solicitacoes;
using Acordu.Application.Tarefas;
using Acordu.Infrastructure.Agendamento;
using Acordu.Infrastructure.Email;
using Acordu.Infrastructure.MultiTenancy;
using Acordu.Infrastructure.Persistence;
using Acordu.Infrastructure.Persistence.Agenda;
using Acordu.Infrastructure.Persistence.Atividades;
using Acordu.Infrastructure.Persistence.Auditoria;
using Acordu.Infrastructure.Persistence.Cadastros;
using Acordu.Infrastructure.Persistence.Colaboracao;
using Acordu.Infrastructure.Persistence.Consultas;
using Acordu.Infrastructure.Persistence.Eventos;
using Acordu.Infrastructure.Persistence.Notificacoes;
using Acordu.Infrastructure.Persistence.Outbox;
using Acordu.Infrastructure.Persistence.Protocolo;
using Acordu.Infrastructure.Seguranca;
using Acordu.Infrastructure.TempoReal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Acordu.Infrastructure;

/// <summary>
/// Registro da Infrastructure no container. O <see cref="ContextoTenant"/> é por escopo e começa vazio: a Api o define a
/// partir do token; jobs o definem para o tenant que processam.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddSingleton(TimeProvider.System);
        services.AddLogging(); // idempotente; jobs e testes de integração sem host também têm ILogger<T>
        services.AddScoped<ContextoTenant>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<ContextoTenant>());
        services.AddDbContext<AcorduDbContext>(options => AcorduDbContextOptions.Configurar(options, connectionString));

        services.AddScoped<DespachanteEventos>();
        services.AddScoped<MensagensDaRequisicao>();
        services.AddSingleton<DifusorSinais>();
        services.AddScoped<OrigemDoEvento>();
        services.AddScoped<IOrigemDoEvento>(sp => sp.GetRequiredService<OrigemDoEvento>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepositorio<>), typeof(Repositorio<>));
        services.AddScoped<IGeradorProtocolo, GeradorProtocolo>();
        services.AddScoped<IControleVersao, ControleVersao>();
        services.AddScoped<ICalendarios, Calendarios>();
        services.AddScoped<IMembrosDaEquipe, MembrosDaEquipe>();
        services.AddScoped<ITenantAtual, TenantAtual>();
        services.AddScoped<IAcessosDoPortal, AcessosDoPortal>();
        services.AddScoped<Application.EntradaEmail.IConversasPorEmail, Persistence.EntradaEmail.ConversasPorEmail>();
        // Entrada por e-mail (F15d): destino → empresa (consulta global revisada) e o registro de entrada no tenant.
        services.AddScoped<Application.EntradaEmail.IEmpresasDeEntrada, Persistence.EntradaEmail.EmpresasDeEntrada>();
        services.AddScoped<Application.EntradaEmail.IEmailsRecebidos, Persistence.EntradaEmail.EmailsRecebidos>();
        services.AddScoped<IVinculos, Vinculos>();
        services.AddScoped<IPessoasDaEquipe, PessoasDaEquipe>();
        services.AddScoped<IMarcadoresDeAviso, MarcadoresDeAviso>();
        services.AddScoped<IAuditoria, TrilhaDeAuditoria>();
        services.TryAddScoped<IOrigemRequisicao, SemOrigemRequisicao>();
        services.AddSingleton<ExpurgoAuditoria>();
        services.AddScoped<INotificacoes, RegistroNotificacoes>();
        services.AddScoped<IPreferenciasNotificacao, PreferenciasNotificacao>();
        services.AddScoped<IEnviosBloqueados, EnviosBloqueados>();
        services.AddScoped<ISeguidores, RegistroSeguidores>();
        services.AddScoped<IAssinaturasAgenda, AssinaturasAgenda>();
        services.AddScoped<IAtividades, RegistroAtividades>();
        services.AddSingleton<ExpurgoNotificacoes>();
        services.AddSingleton<ExpurgoAtividades>();
        services.AddSingleton<ApagadorDeEmpresa>();
        services.AddSingleton<ExpurgoCadastros>();
        services.AddSingleton<Operacao.ExpurgoOcorrenciasErro>();
        // Expurgo dos convites de empresa encerrados (#213): só banco e relógio, como os outros expurgos de retenção.
        services.AddSingleton<Persistence.ConvitesEmpresa.ExpurgoConvitesEmpresa>();
        services.AddSingleton<Operacao.MarcasDeOperacao>();
        services.AddScoped<Application.Sugestoes.ISugestoes, Persistence.Sugestoes.RegistroSugestoes>();
        services.AddSingleton<Application.Abstractions.Operacao.IMarcasDeOperacao>(sp => sp.GetRequiredService<Operacao.MarcasDeOperacao>());
        services.AddScoped<IConvitesEquipe, ConvitesEquipe>();
        services.AddScoped<IConsultasEquipe, ConsultasEquipe>();
        services.AddScoped<IConsultasComentariosTarefa, ConsultasComentariosTarefa>();
        services.AddScoped<IConsultasArquivosCliente, ConsultasArquivosCliente>();
        services.AddScoped<IConsultasCapacidade, ConsultasCapacidade>();
        services.AddScoped<IConsultasPapel, ConsultasPapel>();
        services.AddScoped<IPortalAtual, PortalAtual>();
        services.AddSingleton<ProcessadorOutbox>();
        services.AddSingleton<ExecutorPorTenant>();

        services.AddScoped<ConsultaSql>();
        services.AddScoped<IConsultasCliente, ConsultasCliente>();
        // Dados de exemplo (F14r): o que é exemplo e a remoção, no tenant do contexto.
        services.AddScoped<Application.Exemplos.IDadosDeExemplo, Persistence.Exemplos.DadosDeExemplo>();
        services.AddScoped<IConsultasCategoria, ConsultasCategoria>();
        services.AddScoped<IConsultasContrato, ConsultasContrato>();
        services.AddScoped<IConsultasRecorrencia, ConsultasRecorrencia>();
        services.AddScoped<IConsultasAuditoria, ConsultasAuditoria>();
        services.AddScoped<IConsultasDashboard, ConsultasDashboard>();
        services.AddScoped<IConsultasAgenda, ConsultasAgenda>();
        services.AddScoped<IConsultasNotificacoes, ConsultasNotificacoes>();
        services.AddScoped<IConsultasProjeto, ConsultasProjeto>();
        services.AddScoped<IConsultaExtratoPortal, ConsultaExtratoPortal>();
        services.AddScoped<IConsultasPortal, ConsultasPortal>();
        services.AddScoped<IConsultasLinkPortal, ConsultasLinkPortal>();
        services.AddScoped<IConsultaSemelhantes, ConsultaSemelhantes>();
        services.AddScoped<IConsultasSolicitacao, ConsultasSolicitacao>();
        services.AddScoped<IConsultasTarefa, ConsultasTarefa>();
        services.AddScoped<IConsultasApontamento, ConsultasApontamento>();
        return services;
    }

    /// <summary>
    /// Identity (sem roles: o <c>Papel</c> do domínio cumpre esse papel) e o serviço de sessões. Separado de
    /// <see cref="AddInfrastructure"/> porque só a Api autentica; jobs e testes de integração não precisam.
    /// </summary>
    public static IServiceCollection AddSeguranca(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddIdentityCore<Usuario>(OpcoesIdentidade.Configurar)
            .AddEntityFrameworkStores<AcorduDbContext>();
        services.AddScoped<Persistence.Documentos.RegistroDocumentos>();
        // A liberação da área da Nebrys (F14l) lê o mesmo registro, pelo HMAC.
        services.AddScoped<Application.Nebrys.IDocumentosTeste>(sp => sp.GetRequiredService<Persistence.Documentos.RegistroDocumentos>());
        services.AddScoped<Persistence.Aceites.RegistroAceites>();
        // Convites de empresa (F14n, design D12): o cadastro usa o registro; as ações da área, a porta.
        services.AddScoped<Persistence.ConvitesEmpresa.RegistroConvitesEmpresa>();
        services.AddScoped<Application.Nebrys.IConvitesEmpresa>(sp => sp.GetRequiredService<Persistence.ConvitesEmpresa.RegistroConvitesEmpresa>());
        // Lista de espera (F14o, design D12): a página pública grava pela inscrição; as ações da área, pela porta.
        services.AddScoped<Persistence.ListaEspera.RegistroListaEspera>();
        services.AddScoped<Application.ListaEspera.IInscricoesListaEspera>(sp => sp.GetRequiredService<Persistence.ListaEspera.RegistroListaEspera>());
        services.AddScoped<Application.Nebrys.IListaEspera>(sp => sp.GetRequiredService<Persistence.ListaEspera.RegistroListaEspera>());
        // E-mail descartável (F14p, design D9): o cadastro e a lista de espera recusam; o arquivo versionado é lido uma vez.
        services.AddSingleton<Application.Cadastro.IDominiosDescartaveis>(_ => DominiosDescartaveis.DaListaVersionada());
        services.AddScoped<IServicoAutenticacao, ServicoAutenticacao>();
        // Sessão de operação da Nebrys (ADR 0014): depende de OperadoresNebrys, que a Api lê da configuração.
        services.AddScoped<Application.Nebrys.IServicoOperacao, ServicoOperacao>();
        return services;
    }
}
