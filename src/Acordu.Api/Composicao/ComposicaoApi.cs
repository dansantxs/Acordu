using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Acordu.Api.Endpoints;
using Acordu.Api.EntradaEmail;
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
using Acordu.Application.EntradaEmail;
using Acordu.Application.Nebrys;
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
/// <c>Empresas__DiasFaixaTeste</c> (padrão 7; F14e), <c>Empresas__AvisosAntesDoFim</c> (padrão <c>7,1</c>) e
/// <c>Empresas__AvisoAntesDoExpurgo</c> (padrão 7; F14j),
/// <c>Documentos__ChaveHmac</c> (obrigatória, ≥ 32 bytes; F14g), <c>Nebrys__Contato</c> (canal da Nebrys; F14g),
/// <c>Nebrys__Operadores</c> (e-mails dos operadores da área da Nebrys, separados por vírgula; sem ela, a área responde 404; F14k),
/// <c>TextosLegais__Publicados</c> (<c>true</c> ou <c>false</c>, padrão desligado; F14h, D16),
/// <c>Cadastro__Modo</c> (<c>convite</c> ou <c>aberto</c>; sem ela, <c>convite</c>; F14n, D11) e <c>Cadastro__DiasConvite</c>
/// (padrão 7; F14n, D12), <c>EmailEntrada__Dominio</c> (domínio dos endereços de entrada por e-mail; sem ela, sem endereço;
/// F15b, D2), <c>EmailEntrada__Token</c> (token da rota de entrada por e-mail; sem ele, a rota responde 404; F15d, D5) e
/// <c>EmailEntrada__TamanhoMaximoBytes</c> (padrão 25 MiB; F15d, D12), <c>EmailEntrada__DiasRegistro</c> (padrão 7; F15e, D6) e
/// <c>Email__ResendChave</c>, <c>Email__Remetente</c>, <c>Email__NomeRemetente</c> e <c>Email__Smtp__Host</c>/<c>Email__Smtp__Porta</c> (Mailpit, só em desenvolvimento), <c>Email__RegistrarConteudo</c> (só em
/// desenvolvimento).
/// </summary>
internal static class ComposicaoApi
{
    public const string DocumentoInterno = "interno";
    public const string DocumentoPortal = "portal";
    public const string DocumentoNebrys = "nebrys";
    private const string PrefixoNegocio = "api/v1/";
    private const string PrefixoPortal = "api/v1/portal";
    private const string PrefixoNebrys = "api/v1/nebrys";
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
            DiasPositivos(configuracao, "Empresas:DiasFaixaTeste", OpcoesEmpresas.DiasFaixaTestePadrao),
            ListaDeDias(configuracao, "Empresas:AvisosAntesDoFim", OpcoesEmpresas.AvisosAntesDoFimPadrao),
            DiasPositivos(configuracao, "Empresas:AvisoAntesDoExpurgo", OpcoesEmpresas.AvisoAntesDoExpurgoPadrao)));
        // Expurgo das empresas só leitura (F14i) e avisos de situação (F14j): dependem dos prazos acima, que só a Api lê da
        // configuração.
        services.AddSingleton<Infrastructure.Persistence.Cadastros.ExpurgoEmpresas>();
        services.AddSingleton<Infrastructure.Persistence.Cadastros.AvisosDeSituacao>();
        // Um teste por documento (F14g, design D8): a chave do HMAC é obrigatória, como a do JWT; sem ela a API não sobe.
        services.AddSingleton(ProtecaoDocumento.DaConfiguracao(configuracao["Documentos:ChaveHmac"]));
        // Canal da Nebrys nas recusas e avisos (D15); sem ele, os textos dizem só "fale com a Nebrys".
        services.AddSingleton(new OpcoesNebrys(configuracao["Nebrys:Contato"]));
        // Operadores da área da Nebrys (ADR 0014, item 1): sem a variável, ninguém; e-mail inválido impede a subida.
        services.AddSingleton(OperadoresNebrys.DaConfiguracao(configuracao["Nebrys:Operadores"]));
        // Consultas da área (F14k): dependem do prazo do expurgo acima, que só a Api lê da configuração.
        services.AddScoped<IConsultasNebrys, Infrastructure.Persistence.Nebrys.ConsultasNebrys>();
        // Ações da área (F14l): o operador da requisição (preenchido pela barreira), a trilha sem tenant e a mudança de
        // situação num escopo próprio com o tenant fixado (ADR 0014, itens 5 e 6).
        services.AddScoped<OperadorDaRequisicao>();
        services.AddScoped<IOperadorAtual>(sp => sp.GetRequiredService<OperadorDaRequisicao>());
        services.AddScoped<ITrilhaDaOperacao, Infrastructure.Persistence.Auditoria.TrilhaDaOperacao>();
        services.AddScoped<ISituacaoDasEmpresas, Infrastructure.Persistence.Nebrys.SituacaoDasEmpresas>();
        // Empresa de demonstração (F14s, design D13): no mesmo molde, num escopo próprio com o tenant da demonstração.
        services.AddScoped<IEmpresaDeDemonstracao, Infrastructure.Persistence.Demonstracao.EmpresaDeDemonstracao>();
        // Textos legais publicados (D16): padrão desligado; valor presente e inválido impede a subida (F14-121).
        services.AddSingleton(OpcoesTextosLegais.DaConfiguracao(configuracao["TextosLegais:Publicados"]));
        // Modo do cadastro e validade do convite de empresa (F14n, design D11 e D12): sem o modo, convite (fail-closed);
        // valor presente e inválido impede a subida.
        services.AddSingleton(OpcoesCadastro.DaConfiguracao(configuracao["Cadastro:Modo"], configuracao["Cadastro:DiasConvite"]));
        // Domínio dos endereços de entrada por e-mail (F15b, design do F15 D2): sem ele, nenhuma empresa tem endereço e ligar o
        // recebimento é recusado (F15-94); valor presente e inválido impede a subida.
        // O tamanho máximo do e-mail recebido (F15d, D12): padrão 25 MiB. A retenção do registro de entrada (F15e, D6): padrão
        // 7 dias. O domínio do remetente dos avisos (o mesmo padrão do envio) reconhece o laço (F15e, D12).
        services.AddSingleton(OpcoesEmailEntrada.DaConfiguracao(
            configuracao["EmailEntrada:Dominio"],
            configuracao["EmailEntrada:TamanhoMaximoBytes"],
            configuracao["EmailEntrada:DiasRegistro"],
            configuracao["Email:Remetente"] ?? Infrastructure.Email.OpcoesEmail.RemetentePadrao));
        // A rota de entrada (F15d): destino → empresa e o escopo dela, como os jobs.
        services.AddSingleton<Infrastructure.Email.Entrada.RecebedorDeEmail>();
        // Passo expurgo-email-entrada do diário (F15e, D16): depende da retenção configurada acima.
        services.AddSingleton<Infrastructure.Persistence.EntradaEmail.ExpurgoEmailEntrada>();
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

        // Três documentos: o front do portal só pode gerar tipos das rotas /portal (estrutura do repositório), e a área da
        // Nebrys tem o seu (ADR 0014, item 4), fora do interno.
        services.AddOpenApi(DocumentoInterno, opcoes =>
        {
            opcoes.ShouldInclude = descricao =>
                descricao.RelativePath is { } rota
                && rota.StartsWith(PrefixoNegocio, StringComparison.Ordinal)
                && !rota.StartsWith(PrefixoPortal, StringComparison.Ordinal)
                && !rota.StartsWith(PrefixoNebrys, StringComparison.Ordinal);
            opcoes.AddDocumentTransformer(DeclararBearer);
            opcoes.AddDocumentTransformer(Identificar("Acordu — interno"));
            opcoes.AddSchemaTransformer(VersoesDosTextosLegais);
        });
        services.AddOpenApi(DocumentoPortal, opcoes =>
        {
            opcoes.ShouldInclude = descricao =>
                descricao.RelativePath is { } rota && rota.StartsWith(PrefixoPortal, StringComparison.Ordinal);
            opcoes.AddDocumentTransformer(DeclararBearer);
            opcoes.AddDocumentTransformer(Identificar("Acordu — portal"));
        });
        services.AddOpenApi(DocumentoNebrys, opcoes =>
        {
            opcoes.ShouldInclude = descricao =>
                descricao.RelativePath is { } rota && rota.StartsWith(PrefixoNebrys, StringComparison.Ordinal);
            opcoes.AddDocumentTransformer(DeclararBearerDaOperacao);
            opcoes.AddDocumentTransformer(Identificar("Acordu — Nebrys"));
        });
        return services;
    }

    /// <summary>
    /// Número de dias da configuração (<c>Empresas__DiasDeTeste</c>, <c>Empresas__DiasAteExpurgo</c>,
    /// <c>Empresas__DiasFaixaTeste</c>, <c>Empresas__AvisoAntesDoExpurgo</c>): ausente usa o padrão;
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

    /// <summary>
    /// Lista de dias separados por vírgula (<c>Empresas__AvisosAntesDoFim</c>, ex.: <c>7,1</c>): ausente usa o padrão;
    /// presente com um item inválido (não inteiro ou menor que 1) impede a subida, como em <see cref="DiasPositivos"/>.
    /// </summary>
    private static IReadOnlyList<int> ListaDeDias(IConfiguration configuracao, string chave, IReadOnlyList<int> padrao)
    {
        var texto = configuracao[chave];
        if (string.IsNullOrWhiteSpace(texto))
            return padrao;
        var dias = new List<int>();
        foreach (var item in texto.Split(',', StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor) || valor <= 0)
            {
                throw new InvalidOperationException(
                    $"{chave.Replace(":", "__", StringComparison.Ordinal)} precisa ser uma lista de números inteiros de dias maiores que zero, separados por vírgula.");
            }

            dias.Add(valor);
        }

        return dias;
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
                .AddDocument(DocumentoPortal, "Acordu — portal")
                .AddDocument(DocumentoNebrys, "Acordu — Nebrys"));
        }

        // Não toca o banco: é chamado pelo uptime externo e pelo healthcheck do deploy (ADR 0010 §6).
        app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).ExcludeFromDescription();
        app.MapJobs(app.Configuration);
        app.MapEntradaEmail(app.Configuration);

        var api = app.MapGroup("/api/v1").AddEndpointFilter(DespachoOutbox.FiltrarAsync);
        api.MapAuth();
        api.MapAgendaIcs();
        api.MapErrosWeb();
        api.MapListaEspera();
        api.MapNebrys();
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

    /// <summary>
    /// As versões vigentes dos textos legais (<see cref="TextosLegais"/>, D10) como o <c>enum</c> de <c>versaoTermos</c> e
    /// <c>versaoPrivacidade</c> no cadastro e no aceite de convite da equipe: o teste do SPA confere o arquivo do texto
    /// contra o OpenAPI. A obrigatoriedade não entra no documento: depende de <c>TextosLegais__Publicados</c> (D16).
    /// </summary>
    private static Task VersoesDosTextosLegais(
        OpenApiSchema esquema, OpenApiSchemaTransformerContext contexto, CancellationToken cancellationToken)
    {
        var tipo = contexto.JsonTypeInfo.Type;
        if ((tipo != typeof(RegistroRequest) && tipo != typeof(AceiteConviteEquipeRequest)) || esquema.Properties is null)
            return Task.CompletedTask;
        foreach (var (campo, versao) in new[]
                 {
                     (AceiteDosTextos.CampoTermos, TextosLegais.VersaoTermos),
                     (AceiteDosTextos.CampoPrivacidade, TextosLegais.VersaoPrivacidade),
                 })
        {
            if (esquema.Properties.TryGetValue(campo, out var propriedade) && propriedade is OpenApiSchema versaoEsquema)
                versaoEsquema.Enum = [JsonValue.Create(versao)];
        }

        return Task.CompletedTask;
    }

    /// <summary>Esquema Bearer nos documentos: o Scalar mostra o campo do token e o envia nas chamadas.</summary>
    private static Task DeclararBearer(OpenApiDocument documento, OpenApiDocumentTransformerContext contexto, CancellationToken cancellationToken) =>
        DeclararBearer(documento, "Access token de /api/v1/auth/login ou /auth/registro.");

    /// <summary>Na área da Nebrys, o token é o da sessão de operação.</summary>
    private static Task DeclararBearerDaOperacao(
        OpenApiDocument documento, OpenApiDocumentTransformerContext contexto, CancellationToken cancellationToken) =>
        DeclararBearer(documento, "Access token da sessão de operação (/api/v1/nebrys/sessao).");

    private static Task DeclararBearer(OpenApiDocument documento, string descricao)
    {
        documento.Components ??= new OpenApiComponents();
        documento.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        documento.Components.SecuritySchemes[EsquemaBearer] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = descricao,
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
