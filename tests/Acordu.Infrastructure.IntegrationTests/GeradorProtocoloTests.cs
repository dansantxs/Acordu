using Acordu.Application.Abstractions;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Infrastructure.IntegrationTests;

/// <summary>RF-SOL-02: protocolo sequencial por tenant, sem duplicata nem buraco.</summary>
[Collection(ColecaoBanco.Nome)]
public sealed class GeradorProtocoloTests(BancoFixture banco)
{
    private readonly Guid _tenant = Guid.NewGuid();

    /// <summary>O fluxo do caso de uso: reserva o número, abre a solicitação com ele e salva.</summary>
    private static async Task<int> AbrirSolicitacaoAsync(Servicos servicos, Guid tenant, Guid clienteId)
    {
        await using var escopo = servicos.DaEquipe(tenant);
        var numero = await escopo.ServiceProvider.GetRequiredService<IGeradorProtocolo>().ProximoNumeroSolicitacaoAsync();
        var solicitacao = Solicitacao.Abrir(
            Guid.NewGuid(), tenant, clienteId, numero, "Pedido", null, Prioridade.Media, OrigemSolicitacao.Interna,
            null, null, null, null, null, Dados.Agora, CalendarioUtil.Padrao(tenant));
        escopo.ServiceProvider.GetRequiredService<IRepositorio<Solicitacao>>().Adicionar(solicitacao);
        await escopo.ServiceProvider.GetRequiredService<IUnitOfWork>().SalvarAsync();
        return numero;
    }

    private static async Task<Guid> ClienteAsync(Servicos servicos, Guid tenant)
    {
        await using var escopo = servicos.DaEquipe(tenant);
        var cliente = Dados.Cliente(tenant);
        escopo.ServiceProvider.GetRequiredService<IRepositorio<Cliente>>().Adicionar(cliente);
        await escopo.ServiceProvider.GetRequiredService<IUnitOfWork>().SalvarAsync();
        return cliente.Id;
    }

    [Fact]
    public async Task Proximo_SequencialPorTenant()
    {
        await using var servicos = new Servicos(banco);
        var outroTenant = Guid.NewGuid();
        var cliente = await ClienteAsync(servicos, _tenant);
        var clienteOutro = await ClienteAsync(servicos, outroTenant);

        var numeros = new List<int>();
        for (var i = 0; i < 3; i++)
            numeros.Add(await AbrirSolicitacaoAsync(servicos, _tenant, cliente));
        var primeiroDoOutro = await AbrirSolicitacaoAsync(servicos, outroTenant, clienteOutro);

        numeros.Should().Equal(1, 2, 3);
        primeiroDoOutro.Should().Be(1);
    }

    [Fact]
    public async Task Proximo_CriacoesConcorrentes_SemDuplicataNemBuraco()
    {
        await using var servicos = new Servicos(banco);
        var cliente = await ClienteAsync(servicos, _tenant);

        var numeros = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => AbrirSolicitacaoAsync(servicos, _tenant, cliente)));

        numeros.Order().Should().Equal(Enumerable.Range(1, 12));
        await using var db = banco.DaEquipe(_tenant);
        (await db.Solicitacoes.Select(s => s.Numero).OrderBy(n => n).ToListAsync()).Should().Equal(Enumerable.Range(1, 12));
    }

    [Fact]
    public async Task Proximo_SemSalvar_RollbackDevolveONumero()
    {
        await using var servicos = new Servicos(banco);
        var cliente = await ClienteAsync(servicos, _tenant);

        await using (var abandonado = servicos.DaEquipe(_tenant))
        {
            (await abandonado.ServiceProvider.GetRequiredService<IGeradorProtocolo>().ProximoNumeroSolicitacaoAsync()).Should().Be(1);
        }

        (await AbrirSolicitacaoAsync(servicos, _tenant, cliente)).Should().Be(1);
    }

    [Fact]
    public async Task Proximo_SemTenant_Recusa()
    {
        await using var servicos = new Servicos(banco);
        await using var escopo = servicos.Raiz.CreateAsyncScope();

        var acao = () => escopo.ServiceProvider.GetRequiredService<IGeradorProtocolo>().ProximoNumeroSolicitacaoAsync();

        await acao.Should().ThrowAsync<IsolamentoVioladoException>();
    }
}
