using Acordu.Application.Abstractions.Operacao;
using Acordu.Infrastructure.MultiTenancy;
using Acordu.Infrastructure.Operacao;
using Acordu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acordu.Api.FunctionalTests;

/// <summary>Spec do F13, F13b: sem banco (o erro pode ser ele), o aviso sai assim mesmo, deduplicado em memória.</summary>
public sealed class AvisosDeErroSemBancoTests
{
    private sealed class MensageiroDeTeste : IMensageiroOperacao
    {
        public List<string> Textos { get; } = [];

        public bool Configurado(CanalOperacao canal) => canal == CanalOperacao.Alertas;

        public Task<bool> EnviarAsync(CanalOperacao canal, string texto, CancellationToken cancellationToken = default)
        {
            Textos.Add(texto);
            return Task.FromResult(true);
        }
    }

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public DateTimeOffset Agora { get; set; } = agora;

        public override DateTimeOffset GetUtcNow() => Agora;
    }

    [Fact]
    public async Task AvisarAsync_BancoFora_AvisaUmaVezACada24hNaInstancia()
    {
        var servicos = new ServiceCollection();
        // Porta 1 no loopback: conexão recusada na hora, como um banco fora do ar.
        servicos.AddScoped(_ =>
        {
            var opcoes = new DbContextOptionsBuilder<AcorduDbContext>();
            AcorduDbContextOptions.Configurar(opcoes, "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=2");
            return new AcorduDbContext(opcoes.Options, new ContextoTenant());
        });
        await using var provedor = servicos.BuildServiceProvider();
        var mensageiro = new MensageiroDeTeste();
        var relogio = new RelogioFixo(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
        var avisos = new AvisosDeErro(
            provedor.GetRequiredService<IServiceScopeFactory>(), mensageiro, relogio, new OpcoesAvisosDeErro(), NullLogger<AvisosDeErro>.Instance);
        var erro = new ErroInesperado("acordu-api", "Npgsql.NpgsqlException", "Acordu.X.Y", "GET /api/v1/x", "abc1234");

        await avisos.AvisarAsync(erro);
        await avisos.AvisarAsync(erro);
        relogio.Agora = relogio.Agora.AddHours(24);
        await avisos.AvisarAsync(erro);

        mensageiro.Textos.Should().HaveCount(2);
        mensageiro.Textos[0].Should().StartWith("**Erro novo**").And.Contain("sem banco");
    }
}
