using Acordu.Application.Abstractions;
using Acordu.Application.Apontamentos;
using Acordu.Application.Autenticacao;
using Acordu.Application.Clientes;
using Acordu.Application.Contratos;
using Acordu.Application.Portal.Extrato;
using Acordu.Application.Projetos;
using Acordu.Application.Solicitacoes;
using Acordu.Application.Tarefas;
using Acordu.Infrastructure.MultiTenancy;
using Acordu.Infrastructure.Persistence;
using Acordu.Infrastructure.Persistence.Consultas;
using Acordu.Infrastructure.Persistence.Eventos;
using Acordu.Infrastructure.Persistence.Outbox;
using Acordu.Infrastructure.Persistence.Protocolo;
using Acordu.Infrastructure.Seguranca;
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
        services.AddScoped<ContextoTenant>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<ContextoTenant>());
        services.AddDbContext<AcorduDbContext>(options => AcorduDbContextOptions.Configurar(options, connectionString));

        services.AddScoped<DespachanteEventos>();
        services.AddScoped<MensagensDaRequisicao>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepositorio<>), typeof(Repositorio<>));
        services.AddScoped<IGeradorProtocolo, GeradorProtocolo>();
        services.AddScoped<IControleVersao, ControleVersao>();
        services.AddScoped<ICalendarios, Calendarios>();
        services.AddScoped<IMembrosDaEquipe, MembrosDaEquipe>();
        services.AddSingleton<ProcessadorOutbox>();

        services.AddScoped<ConsultaSql>();
        services.AddScoped<IConsultasCliente, ConsultasCliente>();
        services.AddScoped<IConsultasContrato, ConsultasContrato>();
        services.AddScoped<IConsultasProjeto, ConsultasProjeto>();
        services.AddScoped<IConsultaExtratoPortal, ConsultaExtratoPortal>();
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
        services.AddScoped<IServicoAutenticacao, ServicoAutenticacao>();
        return services;
    }
}
