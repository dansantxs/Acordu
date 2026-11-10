using Acordu.Application.Sugestoes;
using Acordu.Domain.Entities;
using Acordu.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Sugestoes;

internal sealed class SugestaoConfiguration : IEntityTypeConfiguration<Sugestao>
{
    public void Configure(EntityTypeBuilder<Sugestao> builder)
    {
        builder.ToTable("sugestoes");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Numero).UseIdentityAlwaysColumn();
        builder.HasIndex(s => s.Numero).IsUnique();
        builder.Property(s => s.Texto).HasMaxLength(Sugestao.TextoMaximo);
        builder.Property(s => s.Tela).HasMaxLength(Sugestao.TelaMaxima);
        builder.HasIndex(s => new { s.PessoaId, s.CriadaEm });
        builder.HasIndex(s => s.EmpresaId);
        builder.HasIndex(s => s.AvisadaEm);
    }
}

/// <summary>
/// Sugestões globais (RF-SUG-01): sem filtro de tenant (não são dados do prestador) e gravadas direto, fora da unidade
/// de trabalho do caso de uso, porque não há mais nada na transação. O nome da empresa vem do tenant, lido sem o filtro
/// (ponto revisado: só o nome, para o aviso da operação).
/// </summary>
public sealed class RegistroSugestoes : ISugestoes
{
    private readonly AcorduDbContext _db;

    public RegistroSugestoes(AcorduDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<int> GravarAsync(Sugestao sugestao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sugestao);
        _db.Add(sugestao);
        await _db.SaveChangesAsync(cancellationToken);
        return sugestao.Numero;
    }

    public Task<int> DaPessoaDesdeAsync(Guid pessoaId, DateTimeOffset desde, CancellationToken cancellationToken = default) =>
        _db.Set<Sugestao>().CountAsync(s => s.PessoaId == pessoaId && s.CriadaEm > desde, cancellationToken);

    public async Task<SugestaoParaAviso?> ParaAvisoAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await ParaAviso(_db.Set<Sugestao>().Where(s => s.Id == id)).ToListAsync(cancellationToken)).SingleOrDefault();

    public async Task<IReadOnlyList<SugestaoParaAviso>> PendentesAsync(
        DateTimeOffset ate, int maximo, CancellationToken cancellationToken = default) =>
        await ParaAviso(_db.Set<Sugestao>().Where(s => s.AvisadaEm == null && s.CriadaEm <= ate).OrderBy(s => s.Numero).Take(maximo))
            .ToListAsync(cancellationToken);

    public Task MarcarAvisadaAsync(Guid id, DateTimeOffset agora, CancellationToken cancellationToken = default) =>
        _db.Set<Sugestao>().Where(s => s.Id == id && s.AvisadaEm == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AvisadaEm, (DateTimeOffset?)agora), cancellationToken);

    private IQueryable<SugestaoParaAviso> ParaAviso(IQueryable<Sugestao> sugestoes) =>
        from s in sugestoes
        join t in _db.Tenants.IgnoreQueryFilters([FiltrosGlobais.Tenant]) on s.EmpresaId equals (Guid?)t.Id into empresas
        from t in empresas.DefaultIfEmpty()
        select new SugestaoParaAviso(s.Id, s.Numero, s.Texto, s.Tela, t == null ? "(empresa removida)" : t.Nome);
}
