using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Operacao;

/// <summary>
/// Ocorrência de um erro inesperado, pela impressão digital (ADR 0012): global, fora dos tenants, só da operação.
/// Nenhuma rota a lê (F13-12); gravada e lida só por SQL em <see cref="AvisosDeErro"/>, e apagada pelo diário 90 dias
/// depois da última vez.
/// </summary>
public sealed class OcorrenciaErro
{
    // Só o EF materializa: a gravação é por SQL.
    private OcorrenciaErro()
    {
    }

    public string Impressao { get; private set; } = "";

    public string Servico { get; private set; } = "";

    public string Tipo { get; private set; } = "";

    public string? Quadro { get; private set; }

    public string Rota { get; private set; } = "";

    public string Versao { get; private set; } = "";

    public DateTimeOffset PrimeiraEm { get; private set; }

    public DateTimeOffset UltimaEm { get; private set; }

    /// <summary>Último aviso no Discord (nulo: nunca avisado).</summary>
    public DateTimeOffset? AvisadaEm { get; private set; }

    /// <summary>O aviso foi o resumo dos segurados pelo teto, e não conta no teto da hora.</summary>
    public bool PorResumo { get; private set; }

    /// <summary>Devia avisar, mas o teto da hora segurou.</summary>
    public bool Segurada { get; private set; }

    public int Total { get; private set; }

    public int DesdeAviso { get; private set; }
}

internal sealed class OcorrenciaErroConfiguration : IEntityTypeConfiguration<OcorrenciaErro>
{
    /// <summary>Pelo modelo do <c>AcorduDbContext</c>, sem que ele dependa do tipo (F13-12, teste de arquitetura).</summary>
    public static void Aplicar(ModelBuilder modelo) => modelo.ApplyConfiguration(new OcorrenciaErroConfiguration());

    public void Configure(EntityTypeBuilder<OcorrenciaErro> builder)
    {
        builder.ToTable("ocorrencias_erro");
        builder.HasKey(o => o.Impressao);
        builder.Property(o => o.Impressao).HasMaxLength(64);
        builder.Property(o => o.Servico).HasMaxLength(40);
        builder.Property(o => o.Tipo).HasMaxLength(300);
        builder.Property(o => o.Quadro).HasMaxLength(300);
        builder.Property(o => o.Rota).HasMaxLength(300);
        builder.Property(o => o.Versao).HasMaxLength(40);
        builder.HasIndex(o => o.AvisadaEm);
        builder.HasIndex(o => o.UltimaEm);
    }
}
