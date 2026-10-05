using Acordu.Application.Abstractions;
using Acordu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>
/// Um Postgres 17 por execução, com as migrations aplicadas. Os testes não limpam o banco: cada um usa tenants e
/// clientes novos, e os filtros garantem que não enxergam dados uns dos outros.
/// </summary>
public sealed class BancoFixture : IAsyncLifetime, IAsyncDisposable
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = Contexto(Quem.Sistema());
        await db.Database.MigrateAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => _postgres.DisposeAsync().AsTask();

    public ValueTask DisposeAsync() => _postgres.DisposeAsync();

    internal AcorduDbContext Contexto(ITenantContext contexto)
    {
        var builder = new DbContextOptionsBuilder<AcorduDbContext>();
        AcorduDbContextOptions.Configurar(builder, ConnectionString);
        return new AcorduDbContext(builder.Options, contexto);
    }

    internal AcorduDbContext DaEquipe(Guid tenantId) => Contexto(Quem.Equipe(tenantId));

    internal AcorduDbContext DoCliente(Guid tenantId, Guid clienteId) => Contexto(Quem.DoCliente(tenantId, clienteId));
}

[CollectionDefinition(Nome)]
public sealed class ColecaoBanco : ICollectionFixture<BancoFixture>
{
    public const string Nome = "banco";
}
