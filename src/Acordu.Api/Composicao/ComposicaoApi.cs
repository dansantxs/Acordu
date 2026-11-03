using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Acordu.Api.Endpoints;
using Acordu.Api.Erros;
using Acordu.Api.Jobs;
using Acordu.Api.Seguranca;
using Acordu.Application.Abstractions;
using Acordu.Infrastructure;
using Acordu.Infrastructure.Armazenamento;
using Acordu.Infrastructure.Email;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Acordu.Api.Composicao;

/// <summary>
/// Composição única da Api, usada pelo <c>Program</c> e pelos testes funcionais (que montam o mesmo pipeline e penduram
/// endpoints de teste nos grupos). Configuração só por variáveis de ambiente (ADR 0010 §10): <c>ConnectionStrings__Acordu</c>,
/// <c>Jwt__ChaveAssinatura</c>, <c>Jobs__Token</c>, <c>Cors__Origem</c>, <c>App__UrlPublica</c> (links dos e-mails),
/// <c>App__UrlApi</c> (feed ICS, quando a API tem host próprio), <c>Proxy__*</c> (borda, ver <see cref="Seguranca.TokenDeOrigem"/>),
/// <c>TempoReal__Sse</c> (padrão ligado; ADR 0010 §4) e
/// <c>Email__ResendChave</c>, <c>Email__Remetente</c>, <c>Email__NomeRemetente</c> e <c>Email__Smtp__Host</c>/<c>Email__Smtp__Porta</c> (Mailpit, só em desenvolvimento), <c>Email__RegistrarConteudo</c> (só em
/// desenvolvimento).
/// </summary>
internal static class ComposicaoApi
{
    public const string DocumentoInterno = "interno";
    public const string DocumentoPortal = "portal";
    private const string PrefixoNegocio = "api/v1/";
    private const string PrefixoPortal = "api/v1/portal";
    private const string EsquemaBearer = "Bearer";
    private const string UrlPublicaPadrao = "https://acordu.app.br/";

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
        var urlApi = configuracao["App:UrlApi"];
        services.AddSingleton(new EnderecosPublicos(
            new Uri(configuracao["App:UrlPublica"] ?? UrlPublicaPadrao),
            string.IsNullOrEmpty(urlApi) ? null : new Uri(urlApi)));
        AdicionarEmail(services, configuracao);
        AdicionarArmazenamento(services, configuracao);

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
            opcoes.AddDocumentTransformer(Identificar("Acordu — interno"));
        });
        services.AddOpenApi(DocumentoPortal, opcoes =>
        {
            opcoes.ShouldInclude = descricao =>
                descricao.RelativePath is { } rota && rota.StartsWith(PrefixoPortal, StringComparison.Ordinal);
            opcoes.AddDocumentTransformer(DeclararBearer);
            opcoes.AddDocumentTransformer(Identificar("Acordu — portal"));
        });
        return services;
    }

    /// <summary>
    /// Resend quando há <c>Email__ResendChave</c>; senão, SMTP quando há <c>Email__Smtp__Host</c> (o Mailpit do
    /// desenvolvimento); senão, só log. TryAdd: os testes registram antes uma caixa de e-mails em memória.
    /// </summary>
    private static void AdicionarEmail(IServiceCollection services, IConfiguration configuracao)
    {
        var opcoes = new OpcoesEmail(
            configuracao["Email:ResendChave"],
            configuracao["Email:Remetente"] ?? OpcoesEmail.RemetentePadrao,
            configuracao["Email:NomeRemetente"] ?? OpcoesEmail.NomeRemetentePadrao,
            configuracao.GetValue("Email:RegistrarConteudo", false),
            configuracao["Email:Smtp:Host"],
            configuracao.GetValue("Email:Smtp:Porta", 1025));
        services.AddSingleton(opcoes);
        if (opcoes.UsaResend)
        {
            services.AddHttpClient(nameof(EnviadorEmailResend), http => http.Timeout = TimeSpan.FromSeconds(15));
            services.TryAddSingleton<IEnviadorEmail>(sp => new EnviadorEmailResend(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(EnviadorEmailResend)), opcoes));
        }
        else if (opcoes.UsaSmtp)
        {
            services.TryAddSingleton<IEnviadorEmail>(sp => new EnviadorEmailSmtp(opcoes, sp.GetRequiredService<ILogger<EnviadorEmailSmtp>>()));
        }
        else
        {
            services.TryAddSingleton<IEnviadorEmail>(sp => new EnviadorEmailLog(
                sp.GetRequiredService<ILogger<EnviadorEmailLog>>(), opcoes.RegistrarConteudo));
        }
    }

    /// <summary>
    /// Object storage S3 (R2 em produção) quando há <c>Armazenamento__Endpoint</c>; senão, anexos indisponíveis. TryAdd: os testes
    /// registram antes um armazenamento em memória.
    /// </summary>
    private static void AdicionarArmazenamento(IServiceCollection services, IConfiguration configuracao)
    {
        var opcoes = new OpcoesArmazenamento(
            configuracao["Armazenamento:Endpoint"],
            configuracao["Armazenamento:Regiao"] ?? OpcoesArmazenamento.RegiaoPadrao,
            configuracao["Armazenamento:Bucket"] ?? OpcoesArmazenamento.BucketPadrao,
            configuracao["Armazenamento:ChaveAcesso"],
            configuracao["Armazenamento:ChaveSecreta"]);
        if (opcoes.Configurado)
            services.TryAddSingleton<IArmazenamentoArquivos>(_ => new ArmazenamentoS3(opcoes));
        else
            services.TryAddSingleton<IArmazenamentoArquivos, ArmazenamentoNaoConfigurado>();
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

        // Não toca o banco: é chamado pelo uptime externo e pelo healthcheck do deploy (ADR 0010 §6).
        app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).ExcludeFromDescription();
        app.MapJobs(app.Configuration);

        var api = app.MapGroup("/api/v1").AddEndpointFilter(DespachoOutbox.FiltrarAsync);
        api.MapAuth();
        api.MapAgendaIcs();
        var grupos = new GruposApi(
            api,
            api.MapGroup(string.Empty).RequireAuthorization(Politicas.Equipe),
            api.MapGroup("/portal").RequireAuthorization(Politicas.Portal).AddEndpointFilter(SessaoPortal.ConferirAsync));
        grupos.Interno.MapClientes();
        grupos.Interno.MapContratos();
        grupos.Interno.MapSolicitacoes();
        grupos.Interno.MapTarefas();
        grupos.Interno.MapProjetos();
        grupos.Interno.MapRecorrencias();
        grupos.Interno.MapApontamentos();
        grupos.Interno.MapConfiguracoes();
        grupos.Interno.MapEquipe();
        grupos.Interno.MapCapacidade();
        grupos.Interno.MapPapeis();
        grupos.Interno.MapAuditoria();
        grupos.Interno.MapDashboard();
        grupos.Interno.MapSeguidores();
        grupos.Interno.MapAgenda();
        grupos.Interno.MapNotificacoes();
        // Nível 0 do ADR 0010 (§4): SSE desligado por configuração; sem a rota, /eventos responde 404.
        if (app.Configuration.GetValue("TempoReal:Sse", true))
            grupos.Interno.MapEventos();
        grupos.Interno.MapAtividades();
        grupos.Portal.MapPortal();
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

    /// <summary>
    /// Título e versão fixos e sem <c>servers</c>: o documento não depende do host que o gerou (o contrato versionado em
    /// <c>openapi/</c> é o mesmo no teste, no desenvolvimento e em produção; o front usa caminhos relativos).
    /// </summary>
    private static Func<OpenApiDocument, OpenApiDocumentTransformerContext, CancellationToken, Task> Identificar(string titulo) =>
        (documento, _, _) =>
        {
            documento.Info ??= new OpenApiInfo();
            documento.Info.Title = titulo;
            documento.Info.Version = "v1";
            documento.Servers = [];
            return Task.CompletedTask;
        };

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
