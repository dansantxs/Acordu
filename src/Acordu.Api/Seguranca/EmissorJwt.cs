using System.Security.Claims;
using Acordu.Application.Autenticacao;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Acordu.Api.Seguranca;

/// <summary>Transforma a <see cref="Sessao"/> em access token. Permissões vão no token: mudança de papel vale no próximo refresh.</summary>
internal static class EmissorJwt
{
    public static TokenDeAcesso Emitir(Sessao sessao, ConfiguracaoJwt configuracao, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(sessao);
        ArgumentNullException.ThrowIfNull(configuracao);

        var claims = new List<Claim>
        {
            new(ClaimsAcordu.Usuario, sessao.UsuarioId.ToString()),
            new(ClaimsAcordu.Tenant, sessao.TenantId.ToString()),
            new(ClaimsAcordu.Papel, sessao.Papel),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
        };
        claims.AddRange(sessao.Permissoes.Select(p => new Claim(ClaimsAcordu.Permissoes, p)));
        if (sessao.ClienteId is { } cliente)
            claims.Add(new Claim(ClaimsAcordu.Cliente, cliente.ToString()));
        if (sessao.EscopoPortal is { } escopo)
            claims.Add(new Claim(ClaimsAcordu.Escopo, ClaimsAcordu.Texto(escopo)));

        var expiraEm = agora + ConfiguracaoJwt.Validade;
        var descritor = new SecurityTokenDescriptor
        {
            Issuer = ConfiguracaoJwt.Emissor,
            Audience = ConfiguracaoJwt.Audiencia,
            IssuedAt = agora.UtcDateTime,
            NotBefore = agora.UtcDateTime,
            Expires = expiraEm.UtcDateTime,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(configuracao.Chave, SecurityAlgorithms.HmacSha256),
        };
        return new TokenDeAcesso(new JsonWebTokenHandler().CreateToken(descritor), expiraEm);
    }
}
