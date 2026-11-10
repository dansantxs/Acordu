using Acordu.Application.Abstractions;
using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Infrastructure.MultiTenancy;
using Acordu.Infrastructure.Persistence.Agenda;
using Acordu.Infrastructure.Persistence.Atividades;
using Acordu.Infrastructure.Persistence.Auditoria;
using Acordu.Infrastructure.Persistence.Colaboracao;
using Acordu.Infrastructure.Persistence.Configurations;
using Acordu.Infrastructure.Persistence.Conversores;
using Acordu.Infrastructure.Persistence.Notificacoes;
using Acordu.Infrastructure.Persistence.Outbox;
using Acordu.Infrastructure.Persistence.Protocolo;
using Acordu.Infrastructure.Seguranca;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence;

/// <summary>
/// Contexto único do Acordu. Aplica os dois isolamentos do ADR 0002 como filtros globais nomeados e recusa gravar fora do
/// tenant/cliente do contexto (<see cref="IsolamentoVioladoException"/>). Também guarda as tabelas do Identity (globais,
/// sem tenant), para o registro gravar usuário e tenant na mesma transação.
/// </summary>
public sealed class AcorduDbContext : IdentityUserContext<Usuario, Guid>
{
    private readonly ITenantContext _contexto;

    public AcorduDbContext(DbContextOptions<AcorduDbContext> options, ITenantContext contexto)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        _contexto = contexto;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Papel> Papeis => Set<Papel>();

    public DbSet<Vinculo> Vinculos => Set<Vinculo>();

    public DbSet<CalendarioUtil> Calendarios => Set<CalendarioUtil>();

    public DbSet<Cliente> Clientes => Set<Cliente>();

    public DbSet<Categoria> Categorias => Set<Categoria>();

    public DbSet<Contrato> Contratos => Set<Contrato>();

    public DbSet<AjusteSaldo> AjustesSaldo => Set<AjusteSaldo>();

    public DbSet<Solicitacao> Solicitacoes => Set<Solicitacao>();

    public DbSet<Interacao> Interacoes => Set<Interacao>();

    public DbSet<AcessoPortal> AcessosPortal => Set<AcessoPortal>();

    public DbSet<ConvitePortal> ConvitesPortal => Set<ConvitePortal>();

    public DbSet<ConviteEquipe> ConvitesEquipe => Set<ConviteEquipe>();

    public DbSet<Projeto> Projetos => Set<Projeto>();

    public DbSet<Tarefa> Tarefas => Set<Tarefa>();

    public DbSet<ComentarioTarefa> ComentariosTarefa => Set<ComentarioTarefa>();

    public DbSet<Apontamento> Apontamentos => Set<Apontamento>();

    public DbSet<Cronometro> Cronometros => Set<Cronometro>();

    public DbSet<RecorrenciaTarefa> Recorrencias => Set<RecorrenciaTarefa>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GarantirIsolamento();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        GarantirIsolamento();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        var enums = typeof(Domain.Enums.Prioridade).Assembly.GetTypes()
            .Where(t => t.IsEnum && t.Namespace == typeof(Domain.Enums.Prioridade).Namespace);
        foreach (var tipo in enums)
        {
            configurationBuilder.Properties(tipo)
                .HaveConversion(typeof(EnumTextoConverter<>).MakeGenericType(tipo))
                .HaveMaxLength(40);
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);
        IdentidadeConfiguration.Aplicar(builder);
        builder.ApplyConfiguration(new RefreshTokenConfiguration());
        builder.ApplyConfiguration(new TokenContaConfiguration());

        // Busca de solicitações semelhantes (read side §8): trigramas e remoção de acentos.
        builder.HasPostgresExtension("pg_trgm");
        builder.HasPostgresExtension("unaccent");

        builder.ApplyConfiguration(new TenantConfiguration());
        builder.ApplyConfiguration(new PapelConfiguration());
        builder.ApplyConfiguration(new VinculoConfiguration());
        builder.ApplyConfiguration(new CalendarioUtilConfiguration());
        builder.ApplyConfiguration(new ClienteConfiguration());
        builder.ApplyConfiguration(new CategoriaConfiguration());
        builder.ApplyConfiguration(new ContratoConfiguration());
        builder.ApplyConfiguration(new AjusteSaldoConfiguration());
        builder.ApplyConfiguration(new SolicitacaoConfiguration());
        builder.ApplyConfiguration(new InteracaoConfiguration());
        builder.ApplyConfiguration(new AcessoPortalConfiguration());
        builder.ApplyConfiguration(new ConvitePortalConfiguration());
        builder.ApplyConfiguration(new ConviteEquipeConfiguration());
        builder.ApplyConfiguration(new ProjetoConfiguration());
        builder.ApplyConfiguration(new TarefaConfiguration());
        builder.ApplyConfiguration(new ComentarioTarefaConfiguration());
        builder.ApplyConfiguration(new ApontamentoConfiguration());
        builder.ApplyConfiguration(new CronometroConfiguration());
        builder.ApplyConfiguration(new RecorrenciaTarefaConfiguration());
        builder.ApplyConfiguration(new MensagemOutboxConfiguration());
        builder.ApplyConfiguration(new MarcadorAvisoConfiguration());
        builder.ApplyConfiguration(new RegistroAuditoriaConfiguration());
        builder.ApplyConfiguration(new NotificacaoConfiguration());
        builder.ApplyConfiguration(new PreferenciaNotificacaoConfiguration());
        builder.ApplyConfiguration(new SeguidorConfiguration());
        builder.ApplyConfiguration(new AtividadeConfiguration());
        builder.ApplyConfiguration(new AssinaturaAgendaConfiguration());
        builder.ApplyConfiguration(new ContadorConfiguration());
        Acordu.Infrastructure.Operacao.OcorrenciaErroConfiguration.Aplicar(builder);

        AplicarFiltros(builder);
    }

    /// <summary>
    /// Filtro "tenant" em toda raiz (em <see cref="Tenant"/>, pelo próprio Id). Filtro "cliente" no contexto de Cliente:
    /// raízes com ClienteId filtram por ele; a tabela de clientes, pelo próprio Id; vínculos, pelo cliente (o contato só
    /// vê o próprio vínculo e os de colegas do mesmo cliente); Cronometro e ComentarioTarefa (só internos) somem; Categoria, CalendarioUtil e
    /// Papel continuam visíveis.
    /// </summary>
    private void AplicarFiltros(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>().HasQueryFilter(FiltrosGlobais.Tenant, e => e.Id == _contexto.TenantId);
        FiltrarTenant<Papel>(modelBuilder);
        FiltrarTenant<Vinculo>(modelBuilder);
        modelBuilder.Entity<Vinculo>().HasQueryFilter(
            FiltrosGlobais.Cliente, e => !_contexto.EhCliente || e.ClienteId == _contexto.ClienteId);

        FiltrarTenant<CalendarioUtil>(modelBuilder);
        FiltrarTenant<Categoria>(modelBuilder);

        FiltrarTenant<Cliente>(modelBuilder);
        modelBuilder.Entity<Cliente>().HasQueryFilter(
            FiltrosGlobais.Cliente, e => !_contexto.EhCliente || e.Id == _contexto.ClienteId);

        FiltrarTenantECliente<Contrato>(modelBuilder);
        FiltrarTenantECliente<AjusteSaldo>(modelBuilder);
        FiltrarTenantECliente<Solicitacao>(modelBuilder);
        FiltrarTenantECliente<Interacao>(modelBuilder);
        FiltrarTenantECliente<AcessoPortal>(modelBuilder);
        FiltrarTenantECliente<ConvitePortal>(modelBuilder);
        FiltrarTenantECliente<Projeto>(modelBuilder);
        FiltrarTenantECliente<RecorrenciaTarefa>(modelBuilder);

        FiltrarTenant<Tarefa>(modelBuilder);
        modelBuilder.Entity<Tarefa>().HasQueryFilter(
            FiltrosGlobais.Cliente, e => !_contexto.EhCliente || e.ClienteId == _contexto.ClienteId);

        FiltrarTenant<Apontamento>(modelBuilder);
        modelBuilder.Entity<Apontamento>().HasQueryFilter(
            FiltrosGlobais.Cliente, e => !_contexto.EhCliente || e.ClienteId == _contexto.ClienteId);

        // Convite para a equipe: só interno; some no contexto de Cliente.
        FiltrarTenant<ConviteEquipe>(modelBuilder);
        modelBuilder.Entity<ConviteEquipe>().HasQueryFilter(FiltrosGlobais.Cliente, e => !_contexto.EhCliente);

        FiltrarTenant<Cronometro>(modelBuilder);
        modelBuilder.Entity<Cronometro>().HasQueryFilter(FiltrosGlobais.Cliente, e => !_contexto.EhCliente);

        // Comentário de tarefa: só interno, nem em tarefa visível ao cliente.
        FiltrarTenant<ComentarioTarefa>(modelBuilder);
        modelBuilder.Entity<ComentarioTarefa>().HasQueryFilter(FiltrosGlobais.Cliente, e => !_contexto.EhCliente);

        // Auditoria: só a equipe do tenant lê (sem tenant = login com falha, invisível a todos).
        modelBuilder.Entity<RegistroAuditoria>().HasQueryFilter(FiltrosGlobais.Tenant, e => e.TenantId == _contexto.TenantId);
        modelBuilder.Entity<RegistroAuditoria>().HasQueryFilter(FiltrosGlobais.Cliente, e => !_contexto.EhCliente);

        // Sino e preferências: só a equipe do tenant.
        modelBuilder.Entity<Notificacao>().HasQueryFilter(FiltrosGlobais.Tenant, e => e.TenantId == _contexto.TenantId);
        modelBuilder.Entity<Notificacao>().HasQueryFilter(FiltrosGlobais.Cliente, e => !_contexto.EhCliente);
        modelBuilder.Entity<PreferenciaNotificacao>().HasQueryFilter(FiltrosGlobais.Tenant, e => e.TenantId == _contexto.TenantId);
        modelBuilder.Entity<PreferenciaNotificacao>().HasQueryFilter(FiltrosGlobais.Cliente, e => !_contexto.EhCliente);

        // Feed de atividade: só a equipe do tenant.
        modelBuilder.Entity<Atividade>().HasQueryFilter(FiltrosGlobais.Tenant, e => e.TenantId == _contexto.TenantId);
        modelBuilder.Entity<Atividade>().HasQueryFilter(FiltrosGlobais.Cliente, e => !_contexto.EhCliente);

        // Seguidores: só a equipe do tenant.
        modelBuilder.Entity<Seguidor>().HasQueryFilter(FiltrosGlobais.Tenant, e => e.TenantId == _contexto.TenantId);
        modelBuilder.Entity<Seguidor>().HasQueryFilter(FiltrosGlobais.Cliente, e => !_contexto.EhCliente);

        // Link da agenda assinável: só a equipe do tenant (o feed resolve o token ignorando os filtros).
        modelBuilder.Entity<AssinaturaAgendaRegistro>().HasQueryFilter(FiltrosGlobais.Tenant, e => e.TenantId == _contexto.TenantId);
        modelBuilder.Entity<AssinaturaAgendaRegistro>().HasQueryFilter(FiltrosGlobais.Cliente, e => !_contexto.EhCliente);
    }

    private void FiltrarTenant<T>(ModelBuilder modelBuilder)
        where T : EntidadeBase =>
        modelBuilder.Entity<T>().HasQueryFilter(FiltrosGlobais.Tenant, e => e.TenantId == _contexto.TenantId);

    private void FiltrarTenantECliente<T>(ModelBuilder modelBuilder)
        where T : RaizComCliente
    {
        FiltrarTenant<T>(modelBuilder);
        modelBuilder.Entity<T>().HasQueryFilter(
            FiltrosGlobais.Cliente, e => !_contexto.EhCliente || e.ClienteId == _contexto.ClienteId);
    }

    /// <summary>
    /// Toda raiz incluída, alterada ou removida precisa ser do tenant do contexto; no contexto de Cliente, também do
    /// cliente do contexto, e só tipos ligados a cliente podem ser gravados.
    /// </summary>
    private void GarantirIsolamento()
    {
        GarantirIsolamentoDosTenants();
        foreach (var entrada in ChangeTracker.Entries<EntidadeBase>())
        {
            if (entrada.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var entidade = entrada.Entity;
            var nome = entidade.GetType().Name;
            if (_contexto.TenantId is not { } tenant)
                throw new IsolamentoVioladoException($"Gravação de {nome} sem tenant no contexto.");
            if (entidade.TenantId != tenant)
                throw new IsolamentoVioladoException($"Gravação de {nome} de outro tenant.");
            if (!_contexto.EhCliente)
                continue;

            var cliente = ClienteDe(entidade);
            if (cliente is null || cliente != _contexto.ClienteId)
                throw new IsolamentoVioladoException($"Gravação de {nome} fora do cliente do contexto.");
        }
    }

    /// <summary>O próprio <see cref="Tenant"/> só é gravado pela equipe dele (o registro define o contexto antes).</summary>
    private void GarantirIsolamentoDosTenants()
    {
        foreach (var entrada in ChangeTracker.Entries<Tenant>())
        {
            if (entrada.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;
            if (_contexto.TenantId != entrada.Entity.Id || _contexto.EhCliente)
                throw new IsolamentoVioladoException("Gravação de Tenant fora do tenant do contexto.");
        }
    }

    private static Guid? ClienteDe(EntidadeBase entidade) => entidade switch
    {
        Vinculo vinculo => vinculo.ClienteId,
        RaizComCliente raiz => raiz.ClienteId,
        Tarefa tarefa => tarefa.ClienteId,
        Apontamento apontamento => apontamento.ClienteId,
        Cliente cliente => cliente.Id,
        _ => null,
    };
}
