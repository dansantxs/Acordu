using Acordu.Application.Abstractions;
using Acordu.Application.Abstractions.Eventos;
using Acordu.Domain.Entities;
using Acordu.Domain.Events;
using Acordu.Infrastructure.Persistence.Eventos;
using Acordu.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Infrastructure.IntegrationTests;

[Collection(ColecaoBanco.Nome)]
public sealed class UnidadeDeTrabalhoTests(BancoFixture banco)
{
    private static readonly Guid Responsavel = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private readonly Guid _tenant = Guid.NewGuid();

    private async Task<Guid> SalvarClienteAsync(Servicos servicos)
    {
        await using var escopo = servicos.DaEquipe(_tenant);
        var cliente = Dados.Cliente(_tenant);
        escopo.ServiceProvider.GetRequiredService<IRepositorio<Cliente>>().Adicionar(cliente);
        await escopo.ServiceProvider.GetRequiredService<IUnitOfWork>().SalvarAsync();
        return cliente.Id;
    }

    private async Task<List<MensagemOutbox>> OutboxDoTenantAsync()
    {
        await using var db = banco.DaEquipe(_tenant);
        return await db.Set<MensagemOutbox>()
            .Where(m => m.TenantId == _tenant)
            .OrderBy(m => m.Sequencia)
            .ToListAsync();
    }

    [Fact]
    public async Task Salvar_GravaRaizEEventoNaOutbox()
    {
        await using var servicos = new Servicos(banco);
        var clienteId = await SalvarClienteAsync(servicos);
        var solicitacao = Dados.Solicitacao(_tenant, clienteId);

        await using (var escopo = servicos.DaEquipe(_tenant))
        {
            escopo.ServiceProvider.GetRequiredService<IRepositorio<Solicitacao>>().Adicionar(solicitacao);
            await escopo.ServiceProvider.GetRequiredService<IUnitOfWork>().SalvarAsync();
        }

        solicitacao.Eventos.Should().BeEmpty();
        var outbox = await OutboxDoTenantAsync();
        outbox.Should().ContainSingle();
        outbox[0].Tipo.Should().Be(nameof(SolicitacaoAberta));
        outbox[0].OcorridaEm.Should().Be(servicos.Relogio.Agora);
        SerializadorEventos.Desserializar(outbox[0].Tipo, outbox[0].Conteudo).Should().Be(
            new SolicitacaoAberta(solicitacao.Id, _tenant, clienteId, solicitacao.Numero, solicitacao.Origem, solicitacao.AbertaEm));
    }

    [Fact]
    public async Task ManipuladorNaTransacao_AlteraRaizEEncadeiaEventos()
    {
        await using var servicos = new Servicos(banco, s => s.AddScoped<IManipuladorEvento<SolicitacaoAberta>, AtribuiAoAbrir>());
        var clienteId = await SalvarClienteAsync(servicos);
        var solicitacao = Dados.Solicitacao(_tenant, clienteId);

        await using (var escopo = servicos.DaEquipe(_tenant))
        {
            escopo.ServiceProvider.GetRequiredService<IRepositorio<Solicitacao>>().Adicionar(solicitacao);
            await escopo.ServiceProvider.GetRequiredService<IUnitOfWork>().SalvarAsync();
        }

        await using var db = banco.DaEquipe(_tenant);
        (await db.Solicitacoes.SingleAsync(s => s.Id == solicitacao.Id)).ResponsavelId.Should().Be(Responsavel);
        (await OutboxDoTenantAsync()).Select(m => m.Tipo).Should().Equal(nameof(SolicitacaoAberta), nameof(SolicitacaoAtribuida));
    }

    [Fact]
    public async Task ManipuladorFalha_NadaEhGravado()
    {
        await using var servicos = new Servicos(banco, s => s.AddScoped<IManipuladorEvento<SolicitacaoAberta>, Falha>());
        var clienteId = await SalvarClienteAsync(servicos);
        var solicitacao = Dados.Solicitacao(_tenant, clienteId);

        await using (var escopo = servicos.DaEquipe(_tenant))
        {
            escopo.ServiceProvider.GetRequiredService<IRepositorio<Solicitacao>>().Adicionar(solicitacao);
            var acao = () => escopo.ServiceProvider.GetRequiredService<IUnitOfWork>().SalvarAsync();
            await acao.Should().ThrowAsync<InvalidOperationException>().WithMessage("falha proposital");
        }

        await using var db = banco.DaEquipe(_tenant);
        (await db.Solicitacoes.AnyAsync(s => s.Id == solicitacao.Id)).Should().BeFalse();
        (await OutboxDoTenantAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task CicloEntreManipuladores_AbortaSemGravar()
    {
        await using var servicos = new Servicos(banco, s =>
        {
            s.AddScoped<IManipuladorEvento<SolicitacaoAberta>, AtribuiAoAbrir>();
            s.AddScoped<IManipuladorEvento<SolicitacaoAtribuida>, ReatribuiSempre>();
        });
        var clienteId = await SalvarClienteAsync(servicos);
        var solicitacao = Dados.Solicitacao(_tenant, clienteId);

        await using (var escopo = servicos.DaEquipe(_tenant))
        {
            escopo.ServiceProvider.GetRequiredService<IRepositorio<Solicitacao>>().Adicionar(solicitacao);
            var acao = () => escopo.ServiceProvider.GetRequiredService<IUnitOfWork>().SalvarAsync();
            await acao.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage($"*{DespachanteEventos.MaximoRodadas} rodadas*");
        }

        await using var db = banco.DaEquipe(_tenant);
        (await db.Solicitacoes.AnyAsync(s => s.Id == solicitacao.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Repositorio_ObterPorId_RespeitaFiltroDeTenant()
    {
        await using var servicos = new Servicos(banco);
        var clienteId = await SalvarClienteAsync(servicos);

        await using var mesmo = servicos.DaEquipe(_tenant);
        await using var outro = servicos.DaEquipe(Guid.NewGuid());

        (await mesmo.ServiceProvider.GetRequiredService<IRepositorio<Cliente>>().ObterPorIdAsync(clienteId)).Should().NotBeNull();
        (await outro.ServiceProvider.GetRequiredService<IRepositorio<Cliente>>().ObterPorIdAsync(clienteId)).Should().BeNull();
    }

    [Fact]
    public async Task Repositorio_Remover_ExcluiNaUnidadeDeTrabalho()
    {
        await using var servicos = new Servicos(banco);
        var tarefa = Dados.Tarefa(_tenant, null);
        await using (var escopo = servicos.DaEquipe(_tenant))
        {
            escopo.ServiceProvider.GetRequiredService<IRepositorio<Tarefa>>().Adicionar(tarefa);
            await escopo.ServiceProvider.GetRequiredService<IUnitOfWork>().SalvarAsync();
        }

        await using (var escopo = servicos.DaEquipe(_tenant))
        {
            var repositorio = escopo.ServiceProvider.GetRequiredService<IRepositorio<Tarefa>>();
            repositorio.Remover((await repositorio.ObterPorIdAsync(tarefa.Id))!);
            await escopo.ServiceProvider.GetRequiredService<IUnitOfWork>().SalvarAsync();
        }

        await using var db = banco.DaEquipe(_tenant);
        (await db.Tarefas.AnyAsync(t => t.Id == tarefa.Id)).Should().BeFalse();
    }

    private sealed class AtribuiAoAbrir(IRepositorio<Solicitacao> solicitacoes) : IManipuladorEvento<SolicitacaoAberta>
    {
        public async Task TratarAsync(SolicitacaoAberta evento, CancellationToken cancellationToken)
        {
            var solicitacao = await solicitacoes.ObterPorIdAsync(evento.SolicitacaoId, cancellationToken)
                ?? throw new InvalidOperationException("Solicitação recém-adicionada deveria estar rastreada.");
            solicitacao.Atribuir(Responsavel);
        }
    }

    private sealed class ReatribuiSempre(IRepositorio<Solicitacao> solicitacoes) : IManipuladorEvento<SolicitacaoAtribuida>
    {
        public async Task TratarAsync(SolicitacaoAtribuida evento, CancellationToken cancellationToken)
        {
            var solicitacao = (await solicitacoes.ObterPorIdAsync(evento.SolicitacaoId, cancellationToken))!;
            solicitacao.Atribuir(Guid.NewGuid());
        }
    }

    private sealed class Falha : IManipuladorEvento<SolicitacaoAberta>
    {
        public Task TratarAsync(SolicitacaoAberta evento, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("falha proposital");
    }
}
