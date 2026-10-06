using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Infrastructure.Seguranca;

/// <summary>Tabelas do Identity com nomes do Acordu (o padrão seria <c>asp_net_users</c> etc.). Globais, sem tenant.</summary>
internal static class IdentidadeConfiguration
{
    public static void Aplicar(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(usuario =>
        {
            usuario.ToTable("usuarios");
            usuario.Property(u => u.Nome).HasMaxLength(Usuario.TamanhoNome).IsRequired();
        });
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("usuario_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("usuario_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("usuario_tokens");
    }
}
