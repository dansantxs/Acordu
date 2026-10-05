using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Acordu.Api.Erros;
using Acordu.Api.Jobs;
using Acordu.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Scalar.AspNetCore;

namespace Acordu.Api.Composicao;

/// <summary>
/// Composição única da Api, usada pelo <c>Program</c> e pelos testes funcionais (que montam o mesmo pipeline e penduram
/// endpoints de teste no grupo de negócio). Configuração só por variáveis de ambiente (ADR 0005):
/// <c>ConnectionStrings__Acordu</c>, <c>Jobs__Token</c>.
/// </summary>
internal static class ComposicaoApi
{
    public const string DocumentoInterno = "interno";
    public const string DocumentoPortal = "portal";
    private const string PrefixoNegocio = "api/v1/";
    private const string PrefixoPortal = "api/v1/portal";

    public static IServiceCollection AddAcorduApi(this IServiceCollection services, IConfiguration configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        var conexao = configuracao.GetConnectionString("Acordu")
            ?? throw new InvalidOperationException("Connection string 'Acordu' não configurada (ConnectionStrings__Acordu).");
        services.AddInfrastructure(conexao);

        services.AddProblemDetails(opcoes => opcoes.CustomizeProblemDetails = contexto =>
            contexto.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? contexto.HttpContext.TraceIdentifier);
        // Corpo/parâmetro malformado vira BadHttpRequestException também fora de Development, para cair no mesmo
        // Problem Details "validacao" em vez de um 400 genérico.
        services.Configure<RouteHandlerOptions>(opcoes => opcoes.ThrowOnBadRequest = true);
        services.ConfigureHttpJsonOptions(opcoes =>
        {
            opcoes.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
            opcoes.SerializerOptions.Converters.Add(new MesReferenciaJsonConverter());
        });

        // Dois documentos: o front do portal só pode gerar tipos das rotas /portal (estrutura do repositório).
        services.AddOpenApi(DocumentoInterno, opcoes => opcoes.ShouldInclude = descricao =>
            descricao.RelativePath is { } rota
            && rota.StartsWith(PrefixoNegocio, StringComparison.Ordinal)
            && !rota.StartsWith(PrefixoPortal, StringComparison.Ordinal));
        services.AddOpenApi(DocumentoPortal, opcoes => opcoes.ShouldInclude = descricao =>
            descricao.RelativePath is { } rota && rota.StartsWith(PrefixoPortal, StringComparison.Ordinal));
        return services;
    }

    /// <summary>Monta o pipeline e devolve o grupo <c>/api/v1</c>, onde os endpoints de negócio são pendurados.</summary>
    public static RouteGroupBuilder UseAcorduApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandler = ManipuladorExcecoes.TratarAsync });
        app.UseStatusCodePages();
        if (!app.Environment.IsDevelopment())
            app.UseHsts();
        app.Use(HeadersSeguranca.AplicarAsync);

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

        return app.MapGroup("/api/v1").AddEndpointFilter(DespachoOutbox.FiltrarAsync);
    }
}
