using Acordu.Api.Composicao;
using Acordu.Api.FunctionalTests.Apoio;
using Acordu.Api.Seguranca;
using Acordu.Application.Abstractions;
using Acordu.Application.Abstractions.Eventos;
using Acordu.Domain.Acesso;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;
using Acordu.Domain.Exceptions;
using Acordu.Infrastructure.MultiTenancy;
using Acordu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Acordu.Api.FunctionalTests;

/// <summary>
/// A Api montada pela mesma composição do <c>Program</c> (<see cref="ComposicaoApi"/>), sobre um Postgres 17 real, com
/// endpoints de teste pendurados no grupo <c>/api/v1</c>. Até o marco de auth, o tenant vem da rota de teste.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string TokenJobs = "token-dos-testes";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();
    private WebApplication? _app;

    internal RegistroEntregas Entregas { get; } = new();

    internal CaixaDeEmails Emails { get; } = new();

    internal ArmazenamentoDeTeste Arquivos { get; } = new();

    internal RelogioDeTeste Relogio { get; } = new(new DateTimeOffset(2026, 10, 6, 13, 0, 0, TimeSpan.Zero));

    internal WebApplication App => _app ?? throw new InvalidOperationException("Fixture não inicializado.");

    public HttpClient NovoHttp() => App.GetTestClient();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var opcoes = new DbContextOptionsBuilder<AcorduDbContext>();
        AcorduDbContextOptions.Configurar(opcoes, _postgres.GetConnectionString());
        await using (var db = new AcorduDbContext(opcoes.Options, new ContextoTenant()))
            await db.Database.MigrateAsync();

        _app = Construir(new Dictionary<string, string?>());
        MapearEndpointsDeTeste(_app.UseAcorduApi());
        await _app.StartAsync();
    }

    /// <summary>
    /// Outra Api sobre o mesmo banco, com configuração extra (borda, SSE desligado, endereços): para os cenários que
    /// dependem de chave lida na montagem. Quem chama descarta.
    /// </summary>
    internal async Task<WebApplication> NovaApiAsync(
        IReadOnlyDictionary<string, string?> extras,
        Action<GruposApi>? mapear = null,
        Action<WebApplication>? antes = null,
        Action<IServiceCollection>? servicos = null)
    {
        var app = Construir(extras, servicos);
        antes?.Invoke(app);
        var grupos = app.UseAcorduApi();
        mapear?.Invoke(grupos);
        await app.StartAsync();
        return app;
    }

    /// <param name="servicos">Depois da composição da Api: troca de handler de HttpClient, por exemplo.</param>
    private WebApplication Construir(IReadOnlyDictionary<string, string?> extras, Action<IServiceCollection>? servicos = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        var configuracao = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Acordu"] = _postgres.GetConnectionString(),
            ["Jobs:Token"] = TokenJobs,
            ["Jwt:ChaveAssinatura"] = "chave-dos-testes-funcionais-com-mais-de-32-bytes",
            ["RateLimit:Auth:PorMinuto"] = "100000",
            ["RateLimit:Refresh:PorMinuto"] = "100000",
            ["RateLimit:Portal:PorMinuto"] = "100000",
            ["RateLimit:Registro:PorHora"] = "100000",
        };
        foreach (var (chave, valor) in extras)
            configuracao[chave] = valor;
        builder.Configuration.AddInMemoryCollection(configuracao);
        builder.Services.AddSingleton<TimeProvider>(Relogio);
        builder.Services.AddSingleton(Entregas);
        builder.Services.AddSingleton<IEnviadorEmail>(Emails);
        builder.Services.AddSingleton<IArmazenamentoArquivos>(Arquivos);
        builder.Services.AddScoped<IManipuladorPosCommit<SolicitacaoAberta>, ManipuladorDeTeste>();
        builder.Services.AddAcorduApi(builder.Configuration);
        servicos?.Invoke(builder.Services);
        return builder.Build();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    internal AcorduDbContext DaEquipe(Guid tenantId)
    {
        var contexto = new ContextoTenant();
        contexto.DefinirEquipe(tenantId);
        var opcoes = new DbContextOptionsBuilder<AcorduDbContext>();
        AcorduDbContextOptions.Configurar(opcoes, _postgres.GetConnectionString());
        return new AcorduDbContext(opcoes.Options, contexto);
    }

    private static void MapearEndpointsDeTeste(GruposApi grupos)
    {
        var api = grupos.Base;

        // Contexto resolvido do token, visto de uma rota interna e de uma do portal.
        grupos.Interno.MapGet("/teste-auth/contexto", (ITenantContext contexto) => Results.Ok(new
        {
            contexto.TenantId,
            contexto.UsuarioId,
            contexto.Papel,
            contexto.EhCliente,
            Permissoes = contexto.Permissoes.Count,
        }));
        grupos.Interno.MapGet("/teste-auth/contratos", () => Results.Ok()).RequirePermissao(Permissoes.ContratoGerenciar);
        grupos.Portal.MapGet("/teste-auth", (ITenantContext contexto) => Results.Ok(new { contexto.ClienteId, contexto.EscopoPortal }));

        var teste = api.MapGroup("/teste");

        teste.MapGet("/erro/{tipo}", (string tipo) => tipo switch
        {
            "transicao" => throw new TransicaoInvalidaException("Solicitação fechada não aceita interação."),
            "periodo" => throw new PeriodoFechadoException("Outubro/2026 está fechado."),
            "contrato" => throw new ContratoIncoerenteException("Contrato de outro cliente."),
            "concorrencia" => throw new DbUpdateConcurrencyException("versão divergente"),
            // Validação real do domínio: ArgumentException com ParamName "nome".
            "argumento" => Results.Ok(Cliente.Criar(Guid.NewGuid(), Guid.NewGuid(), " ", null, null)),
            "inesperado" => throw new InvalidOperationException("detalhe interno que não pode vazar"),
            _ => Results.Ok(),
        });

        teste.MapPost("/eco", (Eco corpo) => Results.Ok(corpo));

        // Caso de uso mínimo: abre uma solicitação pela unidade de trabalho (gera SolicitacaoAberta na outbox).
        teste.MapPost("/solicitacoes/{tenantId:guid}", async (
            Guid tenantId,
            ContextoTenant contexto,
            IGeradorProtocolo protocolo,
            IRepositorio<Cliente> clientes,
            IRepositorio<Solicitacao> solicitacoes,
            IUnitOfWork unidade,
            TimeProvider relogio) =>
        {
            contexto.DefinirEquipe(tenantId);
            var cliente = Cliente.Criar(Guid.CreateVersion7(), tenantId, "Padaria Bom Grão", null, null);
            clientes.Adicionar(cliente);
            var solicitacao = Solicitacao.Abrir(
                Guid.CreateVersion7(), tenantId, cliente.Id, await protocolo.ProximoNumeroSolicitacaoAsync(), "Banner com data errada",
                null, Prioridade.Media, OrigemSolicitacao.Interna, null, null, null, null, null, relogio.GetUtcNow(),
                CalendarioUtil.Padrao(tenantId));
            solicitacoes.Adicionar(solicitacao);
            await unidade.SalvarAsync();
            return Results.Ok(new { solicitacao.Id });
        });

        api.MapGet("/portal/teste", () => Results.Ok());
    }

    internal sealed record Eco(string Nome, Prioridade Prioridade);
}

/// <summary>Entregas do manipulador pós-commit de teste; tenants em <see cref="FalharPara"/> fazem o manipulador falhar.</summary>
internal sealed class RegistroEntregas
{
    private readonly Lock _trava = new();
    private readonly List<SolicitacaoAberta> _entregues = [];
    private readonly HashSet<Guid> _falharPara = [];

    public IReadOnlyList<SolicitacaoAberta> Entregues
    {
        get
        {
            lock (_trava)
                return [.. _entregues];
        }
    }

    public void FalharPara(Guid tenantId)
    {
        lock (_trava)
            _falharPara.Add(tenantId);
    }

    public void PararDeFalhar(Guid tenantId)
    {
        lock (_trava)
            _falharPara.Remove(tenantId);
    }

    public void Registrar(SolicitacaoAberta evento)
    {
        lock (_trava)
        {
            if (_falharPara.Contains(evento.TenantId))
                throw new InvalidOperationException("e-mail fora do ar");
            _entregues.Add(evento);
        }
    }
}

internal sealed class ManipuladorDeTeste(RegistroEntregas registro) : IManipuladorPosCommit<SolicitacaoAberta>
{
    public Task TratarAsync(SolicitacaoAberta evento, CancellationToken cancellationToken)
    {
        registro.Registrar(evento);
        return Task.CompletedTask;
    }
}

/// <summary>Relógio parado, avançado à mão.</summary>
internal sealed class RelogioDeTeste(DateTimeOffset inicio) : TimeProvider
{
    private long _ticks = inicio.UtcTicks;

    public DateTimeOffset Agora
    {
        get => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);
        set => Interlocked.Exchange(ref _ticks, value.UtcTicks);
    }

    public override DateTimeOffset GetUtcNow() => Agora;
}

[CollectionDefinition(Nome)]
public sealed class ColecaoApi : ICollectionFixture<ApiFixture>
{
    public const string Nome = "api";
}
