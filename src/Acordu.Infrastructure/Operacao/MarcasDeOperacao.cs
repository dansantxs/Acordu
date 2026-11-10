using System.Data.Common;
using Acordu.Application.Abstractions.Operacao;
using Acordu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Acordu.Infrastructure.Operacao;

/// <summary>Marca "já avisado" da operação (ADR 0012): global, fora dos tenants; apagada pelo diário depois de 7 dias.</summary>
public sealed class MarcaOperacao
{
    // Só o EF materializa: a gravação é por SQL.
    private MarcaOperacao()
    {
    }

    public string Chave { get; private set; } = "";

    public DateTimeOffset CriadaEm { get; private set; }
}

internal sealed class MarcaOperacaoConfiguration : IEntityTypeConfiguration<MarcaOperacao>
{
    public static void Aplicar(ModelBuilder modelo) => modelo.ApplyConfiguration(new MarcaOperacaoConfiguration());

    public void Configure(EntityTypeBuilder<MarcaOperacao> builder)
    {
        builder.ToTable("marcas_operacao");
        builder.HasKey(m => m.Chave);
        builder.Property(m => m.Chave).HasMaxLength(200);
        builder.HasIndex(m => m.CriadaEm);
    }
}

/// <summary>
/// <c>INSERT ... ON CONFLICT DO NOTHING</c>: quem inseriu é o primeiro e avisa. Num escopo próprio, como os avisos de
/// erro. Sem banco, devolve <c>true</c> (avisa a mais em vez de calar uma falha).
/// </summary>
public sealed partial class MarcasDeOperacao : IMarcasDeOperacao
{
    public const int RetencaoDias = 7;

    private readonly IServiceScopeFactory _escopos;
    private readonly TimeProvider _relogio;
    private readonly ILogger<MarcasDeOperacao> _logger;

    public MarcasDeOperacao(IServiceScopeFactory escopos, TimeProvider relogio, ILogger<MarcasDeOperacao> logger)
    {
        ArgumentNullException.ThrowIfNull(escopos);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(logger);
        _escopos = escopos;
        _relogio = relogio;
        _logger = logger;
    }

    public async Task<bool> MarcarAsync(string chave, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chave);
        try
        {
            await using var escopo = _escopos.CreateAsyncScope();
            var agora = _relogio.GetUtcNow();
            var inseridas = await escopo.ServiceProvider.GetRequiredService<AcorduDbContext>().Database.ExecuteSqlAsync(
                $"INSERT INTO marcas_operacao (chave, criada_em) VALUES ({chave}, {agora}) ON CONFLICT (chave) DO NOTHING",
                cancellationToken);
            return inseridas == 1;
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or TimeoutException)
        {
            SemBanco(_logger, ex.GetType().Name);
            return true;
        }
    }

    public async Task DesmarcarAsync(string chave, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chave);
        try
        {
            await using var escopo = _escopos.CreateAsyncScope();
            await escopo.ServiceProvider.GetRequiredService<AcorduDbContext>().Database.ExecuteSqlAsync(
                $"DELETE FROM marcas_operacao WHERE chave = {chave}", cancellationToken);
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or TimeoutException)
        {
            SemBanco(_logger, ex.GetType().Name);
        }
    }

    /// <summary>Passo do diário: apaga as marcas com mais de 7 dias (só servem para o dia em que foram feitas).</summary>
    public async Task<int> ExpurgarAsync(CancellationToken cancellationToken = default)
    {
        var limite = _relogio.GetUtcNow().AddDays(-RetencaoDias);
        await using var escopo = _escopos.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<AcorduDbContext>().Set<MarcaOperacao>()
            .Where(m => m.CriadaEm < limite)
            .ExecuteDeleteAsync(cancellationToken);
    }

    [LoggerMessage(EventId = 604, Level = LogLevel.Warning, Message = "Marca de operação sem banco ({Falha}).")]
    private static partial void SemBanco(ILogger logger, string falha);
}
