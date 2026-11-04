using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Acordu.Application.Abstractions;
using Acordu.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace Acordu.Api.Seguranca;

/// <summary>Autenticação JWT, autorização por superfície e permissão, rate limit das rotas de auth e CORS.</summary>
internal static class ComposicaoSeguranca
{
    /// <summary>Login, cadastro, convites, recuperação de senha e reenvio: por IP, contra força bruta.</summary>
    public const string LimiteAuth = "auth";

    /// <summary>
    /// Renovação da sessão: por refresh token (cookie), não por IP. Cada carga de página renova, e um escritório atrás
    /// de um IP só esgotaria o limite do login em uso normal (achado da fumaça, F1c3; decidido em 15/10).
    /// </summary>
    public const string LimiteRenovacao = "renovacao";

    /// <summary>Abertura de solicitação pelo portal (segurança §9): por usuário, ou por IP sem usuário.</summary>
    public const string LimitePortal = "portal";

    /// <summary>
    /// Cadastro (RNF-SEG-10): por IP, o limite de <see cref="LimiteAuth"/> encadeado com um próprio por hora (padrão 3).
    /// O ASP.NET aplica uma política por rota, então as duas janelas ficam num limitador encadeado.
    /// </summary>
    public const string LimiteRegistro = "registro";

    /// <summary>Feed ICS da agenda assinável, por IP (os calendários buscam de tempos em tempos).</summary>
    public const string LimiteAgenda = "agenda";

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

        // IP do cliente para auditoria e rate limit (ADR 0010 §3). Com Proxy__ConfiarEncaminhamento, confia no
        // X-Forwarded-For: um salto por padrão; Proxy__Saltos (2 no Cloud Run: Worker + front do Google) só vale com o token
        // de origem ativo, senão quem chamasse a URL *.run.app direto escolheria o próprio IP. Sem a chave, o da conexão.
        services.AddHttpContextAccessor();
        services.Replace(ServiceDescriptor.Scoped<IOrigemRequisicao, OrigemDaRequisicao>());
        if (configuracao.GetValue("Proxy:ConfiarEncaminhamento", false))
        {
            services.Configure<ForwardedHeadersOptions>(opcoes =>
            {
                opcoes.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                opcoes.ForwardLimit = TokenDeOrigem.Configurado(configuracao) is null
                    ? 1
                    : Math.Max(1, configuracao.GetValue("Proxy:Saltos", 1));
                opcoes.KnownIPNetworks.Clear();
                opcoes.KnownProxies.Clear();
            });
        }

        var limite = configuracao.GetValue("RateLimit:Auth:PorMinuto", 10);
        var limiteRenovacao = configuracao.GetValue("RateLimit:Refresh:PorMinuto", 30);
        var limitePortal = configuracao.GetValue("RateLimit:Portal:PorMinuto", 10);
        var limiteAgenda = configuracao.GetValue("RateLimit:Agenda:PorMinuto", 30);
        var limiteRegistro = configuracao.GetValue("RateLimit:Registro:PorHora", 3);
        services.AddRateLimiter(opcoes =>
        {
            opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            opcoes.AddPolicy(LimitePortal, http => RateLimitPartition.GetFixedWindowLimiter(
                http.User.FindFirst(ClaimsAcordu.Usuario)?.Value ?? http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = limitePortal, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            opcoes.AddPolicy(LimiteAgenda, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = limiteAgenda, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            opcoes.AddPolicy(LimiteRenovacao, http => RateLimitPartition.GetFixedWindowLimiter(
                ChaveDaRenovacao(http),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = limiteRenovacao, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            opcoes.AddPolicy(LimiteRegistro, http => RateLimitPartition.Get(
                http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                _ => RateLimiter.CreateChained(
                    new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions { PermitLimit = limite, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }),
                    new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions { PermitLimit = limiteRegistro, Window = TimeSpan.FromHours(1), QueueLimit = 0 }))));
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

    /// <summary>
    /// Partição do limite de renovação: o hash do refresh token do cookie (o token em si não fica na memória do
    /// limitador); sem cookie, o IP, já que a resposta vai ser 401 de qualquer jeito.
    /// </summary>
    internal static string ChaveDaRenovacao(HttpContext http)
    {
        var token = CookieRefresh.Ler(http.Request);
        if (string.IsNullOrEmpty(token))
            return "ip:" + (http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido");
        return "refresh:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    /// <summary>
    /// Token de origem, cabeçalhos do proxy, CORS, autenticação, contexto de tenant, rate limit e autorização, nessa ordem.
    /// </summary>
    public static void UseSegurancaApi(this WebApplication app)
    {
        app.UseTokenDeOrigem();
        app.UseForwardedHeaders();
        app.UseCors();
        app.UseAuthentication();
        app.Use(ContextoDaRequisicao.DefinirAsync);
        app.UseRateLimiter();
        app.UseAuthorization();
    }
}
