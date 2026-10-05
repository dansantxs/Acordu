using Acordu.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acordu.Infrastructure.Persistence.Configurations;

/// <summary>Mapeamento comum a toda raiz: chave, tenant, eventos fora do modelo e concorrência otimista por <c>xmin</c>.</summary>
internal static class ConfiguracaoRaiz
{
    /// <summary>Propriedade escondida (shadow) com a versão da linha, exposta na API como <c>versao</c>.</summary>
    public const string Versao = "Versao";

    public static void Aplicar<T>(EntityTypeBuilder<T> builder, string tabela)
        where T : EntidadeBase
    {
        builder.ToTable(tabela);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        // Sem índice só em TenantId: cada tabela tem índices compostos que começam por ele.
        builder.Property(e => e.TenantId).IsRequired();
        builder.Ignore(e => e.Eventos);
        builder.Property<uint>(Versao)
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid");
    }

    /// <summary>
    /// <c>ClienteId</c> das raízes ligadas a cliente: obrigatório e indexado junto do tenant (ADR 0002). Quem já tem um
    /// índice mais largo começando por (TenantId, ClienteId) passa <paramref name="indexar"/> falso.
    /// </summary>
    public static void AplicarCliente<T>(EntityTypeBuilder<T> builder, bool indexar = true)
        where T : RaizComCliente
    {
        builder.Property(e => e.ClienteId).IsRequired();
        if (indexar)
            builder.HasIndex(e => new { e.TenantId, e.ClienteId });
    }
}
