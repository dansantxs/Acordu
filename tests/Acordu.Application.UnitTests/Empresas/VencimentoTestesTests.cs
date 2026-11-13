using Acordu.Application.Abstractions;
using Acordu.Application.Empresas;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;

namespace Acordu.Application.UnitTests.Empresas;

/// <summary>Spec do F14, F14-03 a F14-05: o passo <c>vencimento-testes</c> do diário, no tenant do contexto.</summary>
public sealed class VencimentoTestesTests
{
    private static readonly DateTimeOffset Cadastro = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Executar_FimDoTestePassado_VenceSalvaEConta() // F14-03
    {
        var tenant = Novo();
        var (vencimento, unidade) = Montar(tenant, Cadastro.AddDays(30).AddHours(6));

        var contadas = await vencimento.ExecutarAsync();

        contadas.Should().Be(1);
        tenant.Situacao.Should().Be(SituacaoEmpresa.Vencida);
        tenant.SomenteLeituraDesde.Should().Be(Cadastro.AddDays(30).AddHours(6));
        unidade.Salvamentos.Should().Be(1);
    }

    [Fact]
    public async Task Executar_FimDoTesteAindaPorVir_NaoMudaNemSalva() // F14-04
    {
        var tenant = Novo();
        var (vencimento, unidade) = Montar(tenant, Cadastro.AddDays(30).AddSeconds(-1));

        var contadas = await vencimento.ExecutarAsync();

        contadas.Should().Be(0);
        tenant.Situacao.Should().Be(SituacaoEmpresa.Teste);
        unidade.Salvamentos.Should().Be(0);
    }

    [Fact]
    public async Task Executar_FimDoTesteExatamenteAgora_Vence() // F14-03 (borda)
    {
        var tenant = Novo();
        var (vencimento, unidade) = Montar(tenant, Cadastro.AddDays(30));

        (await vencimento.ExecutarAsync()).Should().Be(1);

        tenant.Situacao.Should().Be(SituacaoEmpresa.Vencida);
        tenant.SomenteLeituraDesde.Should().Be(Cadastro.AddDays(30));
        unidade.Salvamentos.Should().Be(1);
    }

    [Fact]
    public async Task Executar_JaVencida_NaoContaDeNovo() // F14-05
    {
        var tenant = Novo();
        tenant.Vencer(Cadastro.AddDays(30));
        var (vencimento, unidade) = Montar(tenant, Cadastro.AddDays(31));

        var contadas = await vencimento.ExecutarAsync();

        contadas.Should().Be(0);
        tenant.SomenteLeituraDesde.Should().Be(Cadastro.AddDays(30), "a data do vencimento não muda");
        unidade.Salvamentos.Should().Be(0);
    }

    private static Tenant Novo() => Tenant.Criar(Guid.NewGuid(), "Estúdio", Cadastro, Guid.NewGuid(), TimeSpan.FromDays(30));

    private static (VencimentoTestes Vencimento, UnidadeContada Unidade) Montar(Tenant tenant, DateTimeOffset agora)
    {
        var unidade = new UnidadeContada();
        return (new VencimentoTestes(new TenantFixo(tenant), new RelogioFixo(agora), unidade), unidade);
    }

    private sealed class TenantFixo(Tenant tenant) : ITenantAtual
    {
        public Task<Tenant> ObterAsync(CancellationToken cancellationToken = default) => Task.FromResult(tenant);
    }

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }

    private sealed class UnidadeContada : IUnitOfWork
    {
        public int Salvamentos { get; private set; }

        public Task SalvarAsync(CancellationToken cancellationToken = default)
        {
            Salvamentos++;
            return Task.CompletedTask;
        }
    }
}
