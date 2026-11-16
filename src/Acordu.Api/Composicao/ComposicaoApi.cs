using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Acordu.Api.Endpoints;
using Acordu.Api.Erros;
using Acordu.Api.Jobs;
using Acordu.Api.Operacao;
using Acordu.Api.Seguranca;
using Acordu.Application.Abstractions;
using Acordu.Application.Abstractions.Operacao;
using Acordu.Application.Operacao;
using Acordu.Application.Autenticacao;
using Acordu.Application.Cadastro;
using Acordu.Application.Empresas;
using Acordu.Infrastructure;
using Acordu.Infrastructure.Armazenamento;
using Acordu.Infrastructure.Email;
using Acordu.Infrastructure.Operacao;
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
/// <c>TempoReal__Sse</c> (padrão ligado; ADR 0010 §4), <c>Empresas__DiasDeTeste</c> e <c>Empresas__DiasAteExpurgo</c> (padrão 30; F14),
/// <c>Documentos__ChaveHmac</c> (obrigatória, ≥ 32 bytes; F14g) e <c>Nebrys__Contato</c> (canal da Nebrys; F14g) e
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
        AmbienteDeExecucao.Conferir();
        services.AddLogsAcordu(configuracao);
        AdicionarOperacao(services, configuracao);
        var conexao = configuracao.GetConnectionString("Acordu")
            ?? throw new InvalidOperationException("Connection string 'Acordu' não configurada (ConnectionStrings__Acordu).");
        services.AddInfrastructure(conexao);
        services.AddSegurancaApi(configuracao);
        services.AddCasosDeUso();
        var urlApi = configuracao["App:UrlApi"];
        services.AddSingleton(new EnderecosPublicos(
            new Uri(configuracao["App:UrlPublica"] ?? UrlPublicaPadrao),
            string.IsNullOrEmpty(urlApi) ? null : new Uri(urlApi)));
        services.AddSingleton(new OpcoesEmpresas(
            DiasPositivos(configuracao, "Empresas:DiasDeTeste", OpcoesEmpresas.DiasDeTestePadrao),
            DiasPositivos(configuracao, "Empresas:DiasAteExpurgo", OpcoesEmpresas.DiasAteExpurgoPadrao),
            DiasPositivos(configuracao, "Empresas:DiasFaixaTeste", OpcoesEmpresas.DiasFaixaTestePadrao)));
        // Um teste por documento (F14g, design D8): a chave do HMAC é obrigatória, como a do JWT; sem ela a API não sobe.
        services.AddSingleton(ProtecaoDocumento.DaConfiguracao(configuracao["Documentos:ChaveHmac"]));
        // Canal da Nebrys nas recusas e avisos (D15); sem ele, os textos dizem só "fale com a Nebrys".
        services.AddSingleton(new OpcoesNebrys(configuracao["Nebrys:Contato"]));
        AdicionarEmail(services, configuracao);
        AdicionarArmazenamento(services, configuracao);
        AdicionarVerificacaoHumana(services, configuracao);

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
    /// Número de dias da configuração (<c>Empresas__DiasDeTeste</c>, <c>Empresas__DiasAteExpurgo</c>,
    /// <c>Empresas__DiasFaixaTeste</c>): ausente usa o padrão;
    /// presente e inválido (não inteiro ou menor que 1) impede a subida, para um prazo errado não valer em silêncio.
    /// </summary>
    private static int DiasPositivos(IConfiguration configuracao, string chave, int padrao)
    {
        var texto = configuracao[chave];
        if (string.IsNullOrWhiteSpace(texto))
            return padrao;
        return int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dias) && dias > 0
            ? dias
            : throw new InvalidOperationException($"{chave.Replace(":", "__", StringComparison.Ordinal)} precisa ser um número inteiro de dias maior que zero.");
    }

    /// <summary>Nome do HttpClient do Discord (os testes trocam o handler por ele).</summary>
    internal const string NomeClienteDiscord = "discord";

    /// <summary>
    /// Avisos de operação (ADR 0012): webhooks do Discord por canal (<c>Discord__Alertas</c>, <c>__Deploy</c>,
    /// <c>__Sugestoes</c>; sem, o canal não envia), teto de avisos de erro por hora (<c>Operacao__AvisosPorHora</c>,
    /// padrão 20) e link do Error Reporting (<c>Operacao__LinkErros</c>). 3 s de espera: o aviso nunca segura a resposta.
    /// </summary>
    private static void AdicionarOperacao(IServiceCollection services, IConfiguration configuracao)
    {
        var discord = new OpcoesDiscord(
            OpcoesDiscord.Webhook(configuracao["Discord:Alertas"], "Discord__Alertas"),
            OpcoesDiscord.Webhook(configuracao["Discord:Deploy"], "Discord__Deploy"),
            OpcoesDiscord.Webhook(configuracao["Discord:Sugestoes"], "Discord__Sugestoes"));
        var teto = int.TryParse(configuracao["Operacao:AvisosPorHora"], CultureInfo.InvariantCulture, out var valor) && valor > 0
            ? valor
            : OpcoesAvisosDeErro.TetoPadrao;
        var link = Uri.TryCreate(configuracao["Operacao:LinkErros"], UriKind.Absolute, out var uri) ? uri : null;

        services.AddHttpClient(NomeClienteDiscord, http => http.Timeout = TimeSpan.FromSeconds(3));
        // O HttpClient registra a URL de cada chamada (Information), e a do webhook é o segredo.
        services.AddLogging(logs => logs.AddFilter($"System.Net.Http.HttpClient.{NomeClienteDiscord}", LogLevel.None));
        services.TryAddSingleton<IMensageiroOperacao>(sp => new MensageiroDiscord(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(NomeClienteDiscord),
            discord,
            sp.GetRequiredService<ILogger<MensageiroDiscord>>()));
        services.TryAddSingleton(new OpcoesAvisosDeErro(teto, link));
        services.TryAddSingleton<IAvisosDeErro, AvisosDeErro>();
        services.TryAddSingleton<AvisosDosJobs>();
    }

    /// <summary>Nome do HttpClient do Turnstile (os testes trocam o handler por ele).</summary>
    internal const string NomeClienteTurnstile = "turnstile";

    /// <summary>
    /// Turnstile no cadastro (RNF-SEG-10) quando há <c>Turnstile__Segredo</c>; senão, desligado (dev, testes, E2E). O
    /// hostname esperado é o de <c>App__UrlPublica</c> (o mesmo padrão dos links). 5 s de espera: depois, o cadastro é recusado.
    /// </summary>
    private static void AdicionarVerificacaoHumana(IServiceCollection services, IConfiguration configuracao)
    {
        var segredo = configuracao["Turnstile:Segredo"];
        if (string.IsNullOrEmpty(segredo))
        {
            services.TryAddSingleton<IVerificacaoHumana, Infrastructure.Seguranca.VerificacaoHumanaDesligada>();
            return;
        }

        var opcoes = new Infrastructure.Seguranca.OpcoesTurnstile(segredo, new Uri(configuracao["App:UrlPublica"] ?? UrlPublicaPadrao).Host);
        services.AddHttpClient(NomeClienteTurnstile, http => http.Timeout = TimeSpan.FromSeconds(5));
        services.TryAddScoped<IVerificacaoHumana>(sp => new Infrastructure.Seguranca.VerificadorTurnstile(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(NomeClienteTurnstile),
            opcoes,
            sp.GetRequiredService<ILogger<Infrastructure.Seguranca.VerificadorTurnstile>>()));
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
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            ExceptionHandler = ManipuladorExcecoes.TratarAsync,
            // No JSON, o ManipuladorExcecoes é o único a registrar a exceção tratada: sem isso, cada 500 viraria dois
            // erros no Error Reporting (um sem o contexto) e cada 4xx de regra de negócio viraria um. A falha do próprio
            // handler continua no log (ADR 0012).
            SuppressDiagnosticsCallback = ComposicaoLogs.EmJson(app.Configuration) ? static _ => true : null,
        });
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
        api.MapErrosWeb();
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
        grupos.Interno.MapSugestoes();
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
