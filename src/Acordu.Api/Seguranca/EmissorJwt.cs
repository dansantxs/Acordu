using System.Security.Claims;
using Acordu.Application.Autenticacao;
using Acordu.Domain.Acesso;
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

        return Assinar(claims, configuracao, agora);
    }

    /// <summary>
    /// Sessão de link (segurança §5.3): sem <c>sub</c> nem papel, com as permissões fixas do link, o acesso e a
    /// solicitação. Mesma validade do access token; sem refresh.
    /// </summary>
    public static TokenDeAcesso EmitirLink(SessaoLink sessao, ConfiguracaoJwt configuracao, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(sessao);
        ArgumentNullException.ThrowIfNull(configuracao);

        var claims = new List<Claim>
        {
            new(ClaimsAcordu.Tenant, sessao.TenantId.ToString()),
            new(ClaimsAcordu.Cliente, sessao.ClienteId.ToString()),
            new(ClaimsAcordu.AcessoPortal, sessao.AcessoPortalId.ToString()),
            new(ClaimsAcordu.Solicitacao, sessao.SolicitacaoId.ToString()),
            new(ClaimsAcordu.Tipo, ClaimsAcordu.TipoLink),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
        };
        claims.AddRange(Permissoes.DoLink.Select(p => new Claim(ClaimsAcordu.Permissoes, p)));
        return Assinar(claims, configuracao, agora);
    }

    /// <summary>
    /// Sessão de operação da Nebrys (ADR 0014, item 2): só o operador (<c>sub</c>) e <c>tipo = OPERACAO</c>, <b>sem</b>
    /// <c>tenant_id</c>, papel nem permissões; 30 min, sem refresh.
    /// </summary>
    public static TokenDeAcesso EmitirOperacao(Guid operadorId, ConfiguracaoJwt configuracao, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        var claims = new List<Claim>
        {
            new(ClaimsAcordu.Usuario, operadorId.ToString()),
            new(ClaimsAcordu.Tipo, ClaimsAcordu.TipoOperacao),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
        };
        return Assinar(claims, configuracao, agora, ConfiguracaoJwt.ValidadeOperacao);
    }

    private static TokenDeAcesso Assinar(
        List<Claim> claims, ConfiguracaoJwt configuracao, DateTimeOffset agora, TimeSpan? validade = null)
    {
        var expiraEm = agora + (validade ?? ConfiguracaoJwt.Validade);
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
