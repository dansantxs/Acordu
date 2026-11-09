using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Application.Solicitacoes;
using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.UnitTests.Solicitacoes;

/// <summary>F11-18 e F11-19: a tarefa tira a solicitação de ABERTA, e só ela; repositório e calendário falsos.</summary>
public sealed class AndamentoPelaTarefaTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    // Terça, 06/10/2026, 10:00 em Brasília.
    private static readonly DateTimeOffset Abertura = new(2026, 10, 6, 13, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Agora = Abertura.AddMinutes(45);

    private readonly Repositorio<Solicitacao> _solicitacoes = new();
    private readonly AndamentoPelaTarefa _manipulador;

    public AndamentoPelaTarefaTests()
    {
        _manipulador = new AndamentoPelaTarefa(_solicitacoes, new RelogioDoTenant(new Calendarios(), new Relogio()));
    }

    private Solicitacao Abrir()
    {
        var politica = new PoliticaSla(Enum.GetValues<Prioridade>()
            .Select(p => new PrazoSla(p, Duracao.FromHoras(4), Duracao.FromHoras(16))));
        var solicitacao = Solicitacao.Abrir(
            Guid.NewGuid(), Tenant, Guid.NewGuid(), 1, "Banner com data errada", null, Prioridade.Media,
            OrigemSolicitacao.Interna, null, null, null, Guid.NewGuid(), politica, Abertura, CalendarioUtil.Padrao(Tenant));
        _solicitacoes.Adicionar(solicitacao);
        return solicitacao;
    }

    private static TarefaStatusAlterada Status(StatusTarefa para, Guid? solicitacaoId) =>
        new(Guid.NewGuid(), Tenant, StatusTarefa.AFazer, para, Guid.NewGuid(), solicitacaoId);

    [Fact]
    public async Task TarefaCriadaNaAberta_PassaAEmAndamento() // F11-18
    {
        var solicitacao = Abrir();

        await _manipulador.TratarAsync(new TarefaCriada(Guid.NewGuid(), Tenant, null, solicitacao.Id), CancellationToken.None);

        solicitacao.Status.Should().Be(StatusSolicitacao.EmAndamento);
    }

    [Fact]
    public async Task TarefaAFazendoNaAberta_PassaAEmAndamento() // F11-19, tarefa de antes do F11-18
    {
        var solicitacao = Abrir();

        await _manipulador.TratarAsync(Status(StatusTarefa.Fazendo, solicitacao.Id), CancellationToken.None);

        solicitacao.Status.Should().Be(StatusSolicitacao.EmAndamento);
    }

    [Theory]
    [InlineData(StatusTarefa.Concluida)]
    [InlineData(StatusTarefa.Cancelada)]
    [InlineData(StatusTarefa.AFazer)]
    public async Task OutroStatusDaTarefa_NaoMuda(StatusTarefa para)
    {
        var solicitacao = Abrir();

        await _manipulador.TratarAsync(Status(para, solicitacao.Id), CancellationToken.None);

        solicitacao.Status.Should().Be(StatusSolicitacao.Aberta);
    }

    [Fact]
    public async Task TarefaSemSolicitacao_NaoProcura()
    {
        var solicitacao = Abrir();

        await _manipulador.TratarAsync(Status(StatusTarefa.Fazendo, null), CancellationToken.None);
        await _manipulador.TratarAsync(new TarefaCriada(Guid.NewGuid(), Tenant, null), CancellationToken.None);

        solicitacao.Status.Should().Be(StatusSolicitacao.Aberta);
    }

    [Fact]
    public async Task SolicitacaoForaDeAberta_NaoMuda()
    {
        var solicitacao = Abrir();
        solicitacao.AguardarCliente(Abertura.AddMinutes(30));

        await _manipulador.TratarAsync(Status(StatusTarefa.Fazendo, solicitacao.Id), CancellationToken.None);

        solicitacao.Status.Should().Be(StatusSolicitacao.AguardandoCliente);
    }

    private sealed class Repositorio<T> : IRepositorio<T>
        where T : EntidadeBase
    {
        private readonly Dictionary<Guid, T> _itens = [];

        public Task<T?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_itens.GetValueOrDefault(id));

        public void Adicionar(T entidade) => _itens[entidade.Id] = entidade;

        public void Remover(T entidade) => _itens.Remove(entidade.Id);
    }

    private sealed class Calendarios : ICalendarios
    {
        public Task<CalendarioUtil> DoTenantAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CalendarioUtil.Padrao(Tenant));

        public Task<CalendarioUtil> ParaEdicaoAsync(CancellationToken cancellationToken = default) => DoTenantAsync(cancellationToken);
    }

    private sealed class Relogio : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Agora;
    }
}
