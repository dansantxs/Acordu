using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Acordu.Api.Endpoints;
using Acordu.Api.Erros;
using Acordu.Api.Jobs;
using Acordu.Api.Seguranca;
using Acordu.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Acordu.Api.Composicao;

/// <summary>
/// Composição única da Api, usada pelo <c>Program</c> e pelos testes funcionais (que montam o mesmo pipeline e penduram
/// endpoints de teste nos grupos). Configuração só por variáveis de ambiente (ADR 0005): <c>ConnectionStrings__Acordu</c>,
/// <c>Jwt__ChaveAssinatura</c>, <c>Jobs__Token</c>, <c>Cors__Origem</c>.
/// </summary>
internal static class ComposicaoApi
{
    public const string DocumentoInterno = "interno";
    public const string DocumentoPortal = "portal";
    private const string PrefixoNegocio = "api/v1/";
    private const string PrefixoPortal = "api/v1/portal";
    private const string EsquemaBearer = "Bearer";

    /// <summary>Tipo do Problem Details para respostas sem corpo (401/403/404/429 do pipeline), contrato §13.</summary>
    private static readonly Dictionary<int, (string Tipo, string Titulo)> TiposPorStatus = new()
    {
        [StatusCodes.Status401Unauthorized] = ("nao-autenticado", "Não autenticado"),
        [StatusCodes.Status403Forbidden] = ("sem-permissao", "Sem permissão"),
        [StatusCodes.Status404NotFound] = ("nao-encontrado", "Não encontrado"),
        [StatusCodes.Status429TooManyRequests] = ("limite-requisicoes", "Muitas requisições"),
    };

    public static IServiceCollection AddAcorduApi(this IServiceCollection services, IConfiguration configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        var conexao = configuracao.GetConnectionString("Acordu")
            ?? throw new InvalidOperationException("Connection string 'Acordu' não configurada (ConnectionStrings__Acordu).");
        services.AddInfrastructure(conexao);
        services.AddSegurancaApi(configuracao);
        services.AddCasosDeUso();

        services.AddProblemDetails(opcoes => opcoes.CustomizeProblemDetails = contexto =>
        {
            Completar(contexto.ProblemDetails);
            contexto.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? contexto.HttpContext.TraceIdentifier;
        });
        // Corpo/parâmetro malformado vira BadHttpRequestException também fora de Development, para cair no mesmo
        // Problem Details "validacao" em vez de um 400 genérico.
        services.Configure<RouteHandlerOptions>(opcoes => opcoes.ThrowOnBadRequest = true);
        services.ConfigureHttpJsonOptions(opcoes =>
        {
            opcoes.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
            opcoes.SerializerOptions.Converters.Add(new MesReferenciaJsonConverter());
        });

        // Dois documentos: o front do portal só pode gerar tipos das rotas /portal (estrutura do repositório).
        services.AddOpenApi(DocumentoInterno, opcoes =>
        {
            opcoes.ShouldInclude = descricao =>
                descricao.RelativePath is { } rota
                && rota.StartsWith(PrefixoNegocio, StringComparison.Ordinal)
                && !rota.StartsWith(PrefixoPortal, StringComparison.Ordinal);
            opcoes.AddDocumentTransformer(DeclararBearer);
        });
        services.AddOpenApi(DocumentoPortal, opcoes =>
        {
            opcoes.ShouldInclude = descricao =>
                descricao.RelativePath is { } rota && rota.StartsWith(PrefixoPortal, StringComparison.Ordinal);
            opcoes.AddDocumentTransformer(DeclararBearer);
        });
        return services;
    }

    /// <summary>
    /// Monta o pipeline, mapeia health, jobs e auth, e devolve os grupos onde os endpoints são pendurados: o
    /// <see cref="GruposApi.Interno"/> exige a equipe; o <see cref="GruposApi.Portal"/>, um contato do cliente.
    /// </summary>
    public static GruposApi UseAcorduApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandler = ManipuladorExcecoes.TratarAsync });
        app.UseStatusCodePages();
        if (!app.Environment.IsDevelopment())
            app.UseHsts();
        app.Use(HeadersSeguranca.AplicarAsync);
        app.UseSegurancaApi();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference(opcoes => opcoes
                .AddDocument(DocumentoInterno, "Acordu — interno")
                .AddDocument(DocumentoPortal, "Acordu — portal"));
        }

        // Não toca o banco: o UptimeRobot chama a cada 5 min e acordaria o Neon (ADR 0005 §5).
        app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).ExcludeFromDescription();
        app.MapJobs(app.Configuration);

        var api = app.MapGroup("/api/v1").AddEndpointFilter(DespachoOutbox.FiltrarAsync);
        api.MapAuth();
        var grupos = new GruposApi(
            api,
            api.MapGroup(string.Empty).RequireAuthorization(Politicas.Equipe),
            api.MapGroup("/portal").RequireAuthorization(Politicas.Portal));
        grupos.Interno.MapClientes();
        grupos.Interno.MapContratos();
        grupos.Interno.MapSolicitacoes();
        grupos.Interno.MapTarefas();
        grupos.Interno.MapProjetos();
        grupos.Interno.MapApontamentos();
        return grupos;
    }

    private static void Completar(ProblemDetails problema)
    {
        if (problema.Status is not { } status || !TiposPorStatus.TryGetValue(status, out var tipo))
            return;
        if (problema.Type is null || problema.Type.StartsWith("https://tools.ietf.org", StringComparison.Ordinal))
        {
            problema.Type = ManipuladorExcecoes.BaseTipos + tipo.Tipo;
            problema.Title = tipo.Titulo;
        }
    }

    /// <summary>Esquema Bearer nos documentos: o Scalar mostra o campo do token e o envia nas chamadas.</summary>
    private static Task DeclararBearer(OpenApiDocument documento, OpenApiDocumentTransformerContext contexto, CancellationToken cancellationToken)
    {
        documento.Components ??= new OpenApiComponents();
        documento.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        documento.Components.SecuritySchemes[EsquemaBearer] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Access token de /api/v1/auth/login ou /auth/registro.",
        };
        documento.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(EsquemaBearer, documento)] = [] }];
        return Task.CompletedTask;
    }
}

/// <summary>
/// Grupos de <c>/api/v1</c>: <see cref="Base"/> sem exigência (auth e testes), <see cref="Interno"/> para a equipe e
/// <see cref="Portal"/> (<c>/api/v1/portal</c>) para contatos do cliente.
/// </summary>
internal sealed record GruposApi(RouteGroupBuilder Base, RouteGroupBuilder Interno, RouteGroupBuilder Portal);
