using Acordu.Application.Abstractions;
using Acordu.Application.Abstractions.Eventos;
using Acordu.Domain.Entities;
using Acordu.Domain.Events;
using Acordu.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acordu.Infrastructure.IntegrationTests;

[Collection(ColecaoBanco.Nome)]
public sealed class ProcessadorOutboxTests(BancoFixture banco)
{
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Registro _registro = new();

    /// <summary>Grava uma tarefa e um apontamento pela unidade de trabalho (gera ApontamentoRegistrado na outbox).</summary>
    private async Task<Apontamento> ApontarAsync(Servicos servicos)
    {
        await using var escopo = servicos.DaEquipe(_tenant);
        var tarefa = Dados.Tarefa(_tenant, null);
        var apontamento = Dados.Apontamento(_tenant, null, tarefa.Id);
        escopo.ServiceProvider.GetRequiredService<IRepositorio<Tarefa>>().Adicionar(tarefa);
        escopo.ServiceProvider.GetRequiredService<IRepositorio<Apontamento>>().Adicionar(apontamento);
        await escopo.ServiceProvider.GetRequiredService<IUnitOfWork>().SalvarAsync();
        return apontamento;
    }

    private async Task<MensagemOutbox> MensagemAsync()
    {
        await using var db = banco.DaEquipe(_tenant);
        return await db.Set<MensagemOutbox>().SingleAsync(m => m.TenantId == _tenant);
    }

    /// <summary>Processa até esvaziar o que está vencido (a outbox é compartilhada pelos testes da coleção).</summary>
    private static async Task ProcessarTudoAsync(Servicos servicos)
    {
        var processador = servicos.Raiz.GetRequiredService<ProcessadorOutbox>();
        while (await processador.ProcessarLoteAsync(50) > 0)
        {
        }
    }

    [Fact]
    public async Task Processar_EntregaEventoIgualNoTenantDaMensagem()
    {
        await using var servicos = new Servicos(banco, s =>
        {
            s.AddSingleton(_registro);
            s.AddScoped<IManipuladorPosCommit<ApontamentoRegistrado>, Registra>();
        });
        var apontamento = await ApontarAsync(servicos);

        await ProcessarTudoAsync(servicos);

        _registro.Entregues.Should().ContainSingle(e => e.Evento.ApontamentoId == apontamento.Id);
        var entregue = _registro.Entregues.Single(e => e.Evento.ApontamentoId == apontamento.Id);
        entregue.Evento.Should().Be(new ApontamentoRegistrado(
            apontamento.Id, _tenant, null, null, apontamento.Data, apontamento.Duracao, apontamento.Faturavel));
        entregue.TenantDoContexto.Should().Be(_tenant);
        (await MensagemAsync()).ProcessadaEm.Should().Be(servicos.Relogio.Agora);
    }

    [Fact]
    public async Task Processar_SemManipulador_MarcaProcessada()
    {
        await using var servicos = new Servicos(banco);
        await ApontarAsync(servicos);

        await ProcessarTudoAsync(servicos);

        (await MensagemAsync()).ProcessadaEm.Should().NotBeNull();
    }

    [Fact]
    public async Task Processar_ManipuladorFalha_AgendaNovaTentativaComEsperaCrescente()
    {
        await using var servicos = new Servicos(banco, s => s.AddScoped<IManipuladorPosCommit<ApontamentoRegistrado>, Falha>());
        await ApontarAsync(servicos);
        var inicio = servicos.Relogio.Agora;

        await ProcessarTudoAsync(servicos);
        var depoisDaPrimeira = await MensagemAsync();
        await ProcessarTudoAsync(servicos); // ainda não venceu: não tenta de novo

        depoisDaPrimeira.ProcessadaEm.Should().BeNull();
        depoisDaPrimeira.Tentativas.Should().Be(1);
        depoisDaPrimeira.Erro.Should().Be("InvalidOperationException: e-mail fora do ar");
        depoisDaPrimeira.ProximaTentativaEm.Should().Be(inicio.AddMinutes(1));
        (await MensagemAsync()).Tentativas.Should().Be(1);

        servicos.Relogio.Agora = inicio.AddMinutes(1);
        await ProcessarTudoAsync(servicos);

        var depoisDaSegunda = await MensagemAsync();
        depoisDaSegunda.Tentativas.Should().Be(2);
        depoisDaSegunda.ProximaTentativaEm.Should().Be(inicio.AddMinutes(3));
    }

    [Fact]
    public async Task Processar_EsgotaTentativas_TiraDaFila()
    {
        await using var servicos = new Servicos(banco, s => s.AddScoped<IManipuladorPosCommit<ApontamentoRegistrado>, Falha>());
        await ApontarAsync(servicos);

        for (var i = 0; i < MensagemOutbox.MaximoTentativas + 2; i++)
        {
            await ProcessarTudoAsync(servicos);
            servicos.Relogio.Agora = servicos.Relogio.Agora.AddHours(1);
        }

        var mensagem = await MensagemAsync();
        mensagem.Tentativas.Should().Be(MensagemOutbox.MaximoTentativas);
        mensagem.FalhouEm.Should().NotBeNull();
        mensagem.ProcessadaEm.Should().BeNull();
    }

    [Fact]
    public async Task Processar_UmaFalha_NaoImpedeAsOutras()
    {
        await using var servicos = new Servicos(banco, s =>
        {
            s.AddSingleton(_registro);
            s.AddScoped<IManipuladorPosCommit<ApontamentoRegistrado>, FalhaUmaVez>();
        });
        await ApontarAsync(servicos);
        await ApontarAsync(servicos);

        await ProcessarTudoAsync(servicos);

        await using var db = banco.DaEquipe(_tenant);
        var mensagens = await db.Set<MensagemOutbox>().Where(m => m.TenantId == _tenant).OrderBy(m => m.Sequencia).ToListAsync();
        mensagens.Should().HaveCount(2);
        mensagens[0].Tentativas.Should().Be(1);
        mensagens[1].ProcessadaEm.Should().NotBeNull();
    }

    private sealed record Entrega(ApontamentoRegistrado Evento, Guid? TenantDoContexto);

    private sealed class Registro
    {
        public List<Entrega> Entregues { get; } = [];

        public bool JaFalhou { get; set; }
    }

    private sealed class Registra(Registro registro, ITenantContext contexto) : IManipuladorPosCommit<ApontamentoRegistrado>
    {
        public Task TratarAsync(ApontamentoRegistrado evento, CancellationToken cancellationToken)
        {
            registro.Entregues.Add(new Entrega(evento, contexto.TenantId));
            return Task.CompletedTask;
        }
    }

    private sealed class Falha : IManipuladorPosCommit<ApontamentoRegistrado>
    {
        public Task TratarAsync(ApontamentoRegistrado evento, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("e-mail fora do ar");
    }

    private sealed class FalhaUmaVez(Registro registro) : IManipuladorPosCommit<ApontamentoRegistrado>
    {
        public Task TratarAsync(ApontamentoRegistrado evento, CancellationToken cancellationToken)
        {
            if (registro.JaFalhou)
                return Task.CompletedTask;

            registro.JaFalhou = true;
            throw new InvalidOperationException("falha só na primeira");
        }
    }
}
