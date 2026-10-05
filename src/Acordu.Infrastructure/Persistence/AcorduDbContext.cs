using Acordu.Application.Abstractions;
using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Infrastructure.MultiTenancy;
using Acordu.Infrastructure.Persistence.Configurations;
using Acordu.Infrastructure.Persistence.Conversores;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Persistence;

/// <summary>
/// Contexto único do Acordu. Aplica os dois isolamentos do ADR 0002 como filtros globais nomeados e recusa gravar fora do
/// tenant/cliente do contexto (<see cref="IsolamentoVioladoException"/>).
/// </summary>
public sealed class AcorduDbContext : DbContext
{
    private readonly ITenantContext _contexto;

    public AcorduDbContext(DbContextOptions<AcorduDbContext> options, ITenantContext contexto)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        _contexto = contexto;
    }

    public DbSet<CalendarioUtil> Calendarios => Set<CalendarioUtil>();

    public DbSet<Cliente> Clientes => Set<Cliente>();

    public DbSet<Categoria> Categorias => Set<Categoria>();

    public DbSet<Contrato> Contratos => Set<Contrato>();

    public DbSet<AjusteSaldo> AjustesSaldo => Set<AjusteSaldo>();

    public DbSet<Solicitacao> Solicitacoes => Set<Solicitacao>();

    public DbSet<Interacao> Interacoes => Set<Interacao>();

    public DbSet<AcessoPortal> AcessosPortal => Set<AcessoPortal>();

    public DbSet<Projeto> Projetos => Set<Projeto>();

    public DbSet<Tarefa> Tarefas => Set<Tarefa>();

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new CalendarioUtilConfiguration());
        modelBuilder.ApplyConfiguration(new ClienteConfiguration());
        modelBuilder.ApplyConfiguration(new CategoriaConfiguration());
        modelBuilder.ApplyConfiguration(new ContratoConfiguration());
        modelBuilder.ApplyConfiguration(new AjusteSaldoConfiguration());
        modelBuilder.ApplyConfiguration(new SolicitacaoConfiguration());
        modelBuilder.ApplyConfiguration(new InteracaoConfiguration());
        modelBuilder.ApplyConfiguration(new AcessoPortalConfiguration());
        modelBuilder.ApplyConfiguration(new ProjetoConfiguration());
        modelBuilder.ApplyConfiguration(new TarefaConfiguration());
        modelBuilder.ApplyConfiguration(new ApontamentoConfiguration());
        modelBuilder.ApplyConfiguration(new CronometroConfiguration());
        modelBuilder.ApplyConfiguration(new RecorrenciaTarefaConfiguration());

        AplicarFiltros(modelBuilder);
    }

    /// <summary>
    /// Filtro "tenant" em toda raiz. Filtro "cliente" no contexto de Cliente: raízes com ClienteId filtram por ele; a
    /// tabela de clientes, pelo próprio Id; Cronometro (só interno) some; Categoria e CalendarioUtil continuam visíveis.
    /// </summary>
    private void AplicarFiltros(ModelBuilder modelBuilder)
    {
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
        FiltrarTenantECliente<Projeto>(modelBuilder);
        FiltrarTenantECliente<RecorrenciaTarefa>(modelBuilder);

        FiltrarTenant<Tarefa>(modelBuilder);
        modelBuilder.Entity<Tarefa>().HasQueryFilter(
            FiltrosGlobais.Cliente, e => !_contexto.EhCliente || e.ClienteId == _contexto.ClienteId);

        FiltrarTenant<Apontamento>(modelBuilder);
        modelBuilder.Entity<Apontamento>().HasQueryFilter(
            FiltrosGlobais.Cliente, e => !_contexto.EhCliente || e.ClienteId == _contexto.ClienteId);

        FiltrarTenant<Cronometro>(modelBuilder);
        modelBuilder.Entity<Cronometro>().HasQueryFilter(FiltrosGlobais.Cliente, e => !_contexto.EhCliente);
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

    private static Guid? ClienteDe(EntidadeBase entidade) => entidade switch
    {
        RaizComCliente raiz => raiz.ClienteId,
        Tarefa tarefa => tarefa.ClienteId,
        Apontamento apontamento => apontamento.ClienteId,
        Cliente cliente => cliente.Id,
        _ => null,
    };
}
