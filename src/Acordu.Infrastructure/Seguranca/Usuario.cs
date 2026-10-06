using Microsoft.AspNetCore.Identity;

namespace Acordu.Infrastructure.Seguranca;

/// <summary>
/// Identidade global (segurança §2): um login por pessoa, gerido pelo ASP.NET Core Identity. Não pertence a um tenant;
/// a participação em cada tenant é o <c>Vinculo</c>. O domínio só conhece o <see cref="IdentityUser{TKey}.Id"/>.
/// </summary>
public sealed class Usuario : IdentityUser<Guid>
{
    public const int TamanhoNome = 200;

    public Usuario(string email, string nome)
    {
        Id = Guid.CreateVersion7();
        UserName = email;
        Email = email;
        Nome = nome;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private Usuario()
    {
        Nome = null!;
    }

    /// <summary>Nome exibido para a equipe e nos apontamentos.</summary>
    public string Nome { get; private set; }

    /// <summary>Tenant em que o usuário entrou por último; o login abre a sessão nele.</summary>
    public Guid? UltimoTenantId { get; set; }
}
