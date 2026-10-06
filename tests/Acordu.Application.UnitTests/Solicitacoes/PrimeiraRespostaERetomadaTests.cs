using Acordu.Application.Abstractions;
using Acordu.Application.Solicitacoes;
using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.UnitTests.Solicitacoes;

/// <summary>Modelo §3.1: primeira resposta e retomada a partir da interação, com repositório e calendário falsos.</summary>
public sealed class PrimeiraRespostaERetomadaTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Cliente = Guid.NewGuid();

    // Terça, 06/10/2026, 10:00 em Brasília.
    private static readonly DateTimeOffset Abertura = new(2026, 10, 6, 13, 0, 0, TimeSpan.Zero);

    private readonly Repositorio<Solicitacao> _solicitacoes = new();
    private readonly PrimeiraRespostaERetomada _manipulador;

    public PrimeiraRespostaERetomadaTests()
    {
        _manipulador = new PrimeiraRespostaERetomada(_solicitacoes, new CalendarioPadrao());
    }

    private Solicitacao Abrir()
    {
        var politica = new PoliticaSla(Enum.GetValues<Prioridade>()
            .Select(p => new PrazoSla(p, Duracao.FromHoras(4), Duracao.FromHoras(16))));
        var solicitacao = Solicitacao.Abrir(
            Guid.NewGuid(), Tenant, Cliente, 1, "Banner com data errada", null, Prioridade.Media, OrigemSolicitacao.Interna,
            null, null, null, Guid.NewGuid(), politica, Abertura, CalendarioUtil.Padrao(Tenant));
        _solicitacoes.Adicionar(solicitacao);
        return solicitacao;
    }

    private static InteracaoRegistrada Interacao(Solicitacao solicitacao, bool doCliente, Visibilidade visibilidade, int minutos) =>
        new(Guid.NewGuid(), Tenant, Cliente, solicitacao.Id, Guid.NewGuid(), doCliente, visibilidade, Abertura.AddMinutes(minutos));

    [Fact]
    public async Task ExternaDaEquipe_RegistraPrimeiraRespostaUmaVez()
    {
        var solicitacao = Abrir();

        await _manipulador.TratarAsync(Interacao(solicitacao, false, Visibilidade.Externa, 40), CancellationToken.None);
        await _manipulador.TratarAsync(Interacao(solicitacao, false, Visibilidade.Externa, 90), CancellationToken.None);

        solicitacao.PrimeiraRespostaEm.Should().Be(Abertura.AddMinutes(40));
    }

    [Fact]
    public async Task InternaDaEquipe_NaoContaComoResposta()
    {
        var solicitacao = Abrir();

        await _manipulador.TratarAsync(Interacao(solicitacao, false, Visibilidade.Interna, 40), CancellationToken.None);

        solicitacao.PrimeiraRespostaEm.Should().BeNull();
    }

    [Fact]
    public async Task ClienteRespondeAguardandoCliente_Retoma()
    {
        var solicitacao = Abrir();
        solicitacao.AguardarCliente(Abertura.AddMinutes(30));

        await _manipulador.TratarAsync(Interacao(solicitacao, true, Visibilidade.Externa, 120), CancellationToken.None);

        solicitacao.Status.Should().Be(StatusSolicitacao.EmAndamento);
        solicitacao.Sla!.Pausado.Should().BeFalse();
        solicitacao.PrimeiraRespostaEm.Should().BeNull("resposta do cliente não é a primeira resposta da equipe");
    }

    [Fact]
    public async Task ClienteRespondeEmAndamento_NadaMuda()
    {
        var solicitacao = Abrir();
        solicitacao.Iniciar(Abertura.AddMinutes(10));

        await _manipulador.TratarAsync(Interacao(solicitacao, true, Visibilidade.Externa, 20), CancellationToken.None);

        solicitacao.Status.Should().Be(StatusSolicitacao.EmAndamento);
    }

    [Fact]
    public async Task SolicitacaoFinalizada_NaoMuda()
    {
        var solicitacao = Abrir();
        solicitacao.Cancelar("Duplicada", Abertura.AddMinutes(5));

        var acao = () => _manipulador.TratarAsync(Interacao(solicitacao, false, Visibilidade.Externa, 10), CancellationToken.None);

        await acao.Should().NotThrowAsync();
        solicitacao.PrimeiraRespostaEm.Should().BeNull();
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

    private sealed class CalendarioPadrao : ICalendarios
    {
        public Task<CalendarioUtil> DoTenantAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CalendarioUtil.Padrao(Tenant));
    }
}
