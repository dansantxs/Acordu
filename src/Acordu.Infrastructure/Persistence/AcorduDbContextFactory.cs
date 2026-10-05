using Acordu.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Acordu.Infrastructure.Persistence;

/// <summary>
/// Usado pelo <c>dotnet ef</c> para gerar migrations sem subir a Api. A connection string só importa para comandos que
/// tocam o banco (<c>database update</c>); vem de <c>ACORDU_CONNECTION</c> ou do padrão do docker-compose.dev.
/// </summary>
public sealed class AcorduDbContextFactory : IDesignTimeDbContextFactory<AcorduDbContext>
{
    private const string ConexaoDev = "Host=localhost;Port=5432;Database=acordu;Username=acordu;Password=acordu_dev";

    public AcorduDbContext CreateDbContext(string[] args)
    {
        var conexao = Environment.GetEnvironmentVariable("ACORDU_CONNECTION") ?? ConexaoDev;
        var options = AcorduDbContextOptions.Configurar(new DbContextOptionsBuilder<AcorduDbContext>(), conexao);
        return new AcorduDbContext((DbContextOptions<AcorduDbContext>)options.Options, new SemTenant());
    }

    private sealed class SemTenant : ITenantContext
    {
        public Guid? TenantId => null;

        public bool EhCliente => false;

        public Guid? ClienteId => null;
    }
}
