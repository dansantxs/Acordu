using Microsoft.AspNetCore.Identity;

namespace Acordu.Infrastructure.Seguranca;

/// <summary>Política de senha e de bloqueio (segurança §4): tamanho mínimo sem regra de composição; 5 falhas, 15 min.</summary>
public static class OpcoesIdentidade
{
    public const int TamanhoMinimoSenha = 10;
    public const int TentativasAteBloquear = 5;
    public static readonly TimeSpan DuracaoBloqueio = TimeSpan.FromMinutes(15);

    public static void Configurar(IdentityOptions opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);
        opcoes.Password.RequiredLength = TamanhoMinimoSenha;
        opcoes.Password.RequireDigit = false;
        opcoes.Password.RequireLowercase = false;
        opcoes.Password.RequireUppercase = false;
        opcoes.Password.RequireNonAlphanumeric = false;
        opcoes.Password.RequiredUniqueChars = 1;
        opcoes.User.RequireUniqueEmail = true;
        opcoes.Lockout.AllowedForNewUsers = true;
        opcoes.Lockout.MaxFailedAccessAttempts = TentativasAteBloquear;
        opcoes.Lockout.DefaultLockoutTimeSpan = DuracaoBloqueio;
    }
}
