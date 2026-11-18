using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Acordu.Api.Seguranca;

/// <summary>
/// Assinatura do access token: HS256 com chave própria do Acordu (<c>Jwt__ChaveAssinatura</c>, ≥ 32 bytes; trocar a
/// chave derruba todas as sessões). Validade de 60 min (segurança §5.1).
/// </summary>
internal sealed record ConfiguracaoJwt(SymmetricSecurityKey Chave)
{
    public const string Emissor = "acordu";
    public const string Audiencia = "acordu";
    public static readonly TimeSpan Validade = TimeSpan.FromMinutes(60);

    /// <summary>Sessão de operação da Nebrys (ADR 0014, item 2): mais curta, sem refresh.</summary>
    public static readonly TimeSpan ValidadeOperacao = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan Tolerancia = TimeSpan.FromSeconds(30);

    public static ConfiguracaoJwt De(IConfiguration configuracao)
    {
        var chave = configuracao["Jwt:ChaveAssinatura"];
        if (string.IsNullOrEmpty(chave) || Encoding.UTF8.GetByteCount(chave) < 32)
            throw new InvalidOperationException("Jwt:ChaveAssinatura ausente ou com menos de 32 bytes (Jwt__ChaveAssinatura).");
        return new ConfiguracaoJwt(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chave)));
    }
}
