using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Infrastructure.Persistence.Conversores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Acordu.Infrastructure.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Nome).HasMaxLength(LimitesTexto.Nome).IsRequired();
        builder.Property(t => t.NomePortal).HasMaxLength(LimitesTexto.Nome);
        builder.Property(t => t.DiasFechamentoAutomatico);
        builder.Property(t => t.LogoChave).HasMaxLength(Tenant.TamanhoLogoChave);
        builder.Property(t => t.ResponderPara).HasMaxLength(LimitesTexto.Email);
        // Conversor não genérico: o EF só converte valores não nulos, e a coluna aceita nulo (sem SLA próprio).
        builder.Property(t => t.SlaPadrao).HasConversion((ValueConverter)new PoliticaSlaConverter()).HasColumnType("jsonb");
        builder.Ignore(t => t.SlaSugerido);
        builder.Ignore(t => t.EnvioLiberado);
        // Sem FK para o usuário: a identidade fica fora do domínio, e o expurgo (F9d) apaga os dois juntos.
        builder.Property(t => t.CriadoPorUsuarioId);
        builder.Property(t => t.EnvioLiberadoEm);
        builder.Property<uint>(ConfiguracaoRaiz.Versao)
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid");
    }
}
