using System.Threading.RateLimiting;
using Acordu.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Acordu.Api.Seguranca;

/// <summary>Autenticação JWT, autorização por superfície e permissão, rate limit das rotas de auth e CORS.</summary>
internal static class ComposicaoSeguranca
{
    public const string LimiteAuth = "auth";

    /// <summary>Abertura de solicitação pelo portal (segurança §9): por usuário, ou por IP sem usuário.</summary>
    public const string LimitePortal = "portal";

    public static IServiceCollection AddSegurancaApi(this IServiceCollection services, IConfiguration configuracao)
    {
        var jwt = ConfiguracaoJwt.De(configuracao);
        services.AddSingleton(jwt);
        services.AddSeguranca();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<TimeProvider>((opcoes, relogio) =>
            {
                opcoes.MapInboundClaims = false;
                opcoes.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = ConfiguracaoJwt.Emissor,
                    ValidAudience = ConfiguracaoJwt.Audiencia,
                    IssuerSigningKey = jwt.Chave,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = ClaimsAcordu.Usuario,
                    ClockSkew = ConfiguracaoJwt.Tolerancia,
                    // Validade pelo mesmo relógio que emite (TimeProvider), não pelo relógio da máquina.
                    LifetimeValidator = (inicio, fim, _, _) =>
                    {
                        var agora = relogio.GetUtcNow().UtcDateTime;
                        return (inicio is null || inicio.Value <= agora + ConfiguracaoJwt.Tolerancia)
                            && fim is not null && fim.Value > agora - ConfiguracaoJwt.Tolerancia;
                    },
                };
            });
        services.AddAuthorization(Politicas.Registrar);

        var limite = configuracao.GetValue("RateLimit:Auth:PorMinuto", 10);
        var limitePortal = configuracao.GetValue("RateLimit:Portal:PorMinuto", 10);
        services.AddRateLimiter(opcoes =>
        {
            opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            opcoes.AddPolicy(LimitePortal, http => RateLimitPartition.GetFixedWindowLimiter(
                http.User.FindFirst(ClaimsAcordu.Usuario)?.Value ?? http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = limitePortal, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            opcoes.AddPolicy(LimiteAuth, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = limite, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });

        var origem = configuracao["Cors:Origem"];
        services.AddCors(opcoes =>
        {
            if (!string.IsNullOrEmpty(origem))
                opcoes.AddDefaultPolicy(p => p.WithOrigins(origem).AllowCredentials().AllowAnyHeader().AllowAnyMethod());
        });
        return services;
    }

    /// <summary>CORS, autenticação, contexto de tenant, rate limit e autorização, nessa ordem.</summary>
    public static void UseSegurancaApi(this WebApplication app)
    {
        app.UseCors();
        app.UseAuthentication();
        app.Use(ContextoDaRequisicao.DefinirAsync);
        app.UseRateLimiter();
        app.UseAuthorization();
    }
}
