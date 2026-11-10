using Acordu.Application.Abstractions.Operacao;
using Acordu.Application.Operacao;

namespace Acordu.Application.UnitTests.Operacao;

/// <summary>Spec do F13, F13c: falhas dos jobs em #alertas e o resumo do diário em #deploy (F13-13, F13-14).</summary>
public sealed class AvisosDosJobsTests
{
    // Terça, 10/11/2026, 23:30 em Brasília (02:30 UTC do dia 11).
    private static readonly DateTimeOffset Noite = new(2026, 11, 11, 2, 30, 0, TimeSpan.Zero);

    private sealed class Relogio(DateTimeOffset agora) : TimeProvider
    {
        public DateTimeOffset Agora { get; set; } = agora;

        public override DateTimeOffset GetUtcNow() => Agora;
    }

    private sealed class Mensageiro(params CanalOperacao[] configurados) : IMensageiroOperacao
    {
        public List<(CanalOperacao Canal, string Texto)> Enviadas { get; } = [];

        public bool Falhar { get; set; }

        public bool Configurado(CanalOperacao canal) => configurados.Contains(canal);

        public Task<bool> EnviarAsync(CanalOperacao canal, string texto, CancellationToken cancellationToken = default)
        {
            if (Falhar)
                return Task.FromResult(false);
            Enviadas.Add((canal, texto));
            return Task.FromResult(true);
        }
    }

    private sealed class Marcas : IMarcasDeOperacao
    {
        public HashSet<string> Feitas { get; } = [];

        public Task<bool> MarcarAsync(string chave, CancellationToken cancellationToken = default) => Task.FromResult(Feitas.Add(chave));

        public Task DesmarcarAsync(string chave, CancellationToken cancellationToken = default)
        {
            Feitas.Remove(chave);
            return Task.CompletedTask;
        }
    }

    private readonly Relogio _relogio = new(Noite);
    private readonly Marcas _marcas = new();

    private AvisosDosJobs Avisos(Mensageiro mensageiro) => new(mensageiro, _marcas, _relogio);

    [Fact]
    public async Task FalhasAsync_PassoComFalha_UmaMensagemPorPassoEmAlertas() // F13-13
    {
        var mensageiro = new Mensageiro(CanalOperacao.Alertas);

        var enviadas = await Avisos(mensageiro).FalhasAsync("tick", [new("recorrencias", 0), new("alertas-sla", 3), new("cronometros-esquecidos", 1)]);

        enviadas.Should().Be(2);
        mensageiro.Enviadas.Should().Equal(
            (CanalOperacao.Alertas, "**Job tick**: 3 falhas em `alertas-sla` hoje. Detalhes no log."),
            (CanalOperacao.Alertas, "**Job tick**: 1 falha em `cronometros-esquecidos` hoje. Detalhes no log."));
        _marcas.Feitas.Should().Contain("job:tick:alertas-sla:2026-11-10", "o dia é o de Brasília, não o UTC");
    }

    [Fact]
    public async Task FalhasAsync_MesmoPassoNoMesmoDia_NaoRepete() // F13-13
    {
        var mensageiro = new Mensageiro(CanalOperacao.Alertas);
        var avisos = Avisos(mensageiro);

        await avisos.FalhasAsync("tick", [new("alertas-sla", 1)]);
        await avisos.FalhasAsync("tick", [new("alertas-sla", 2)]);
        _relogio.Agora = Noite.AddHours(1); // 00:30 do dia 11 em Brasília
        await avisos.FalhasAsync("tick", [new("alertas-sla", 1)]);

        mensageiro.Enviadas.Should().HaveCount(2);
    }

    [Fact]
    public async Task FalhasAsync_AvisoNaoSaiu_DesfazAMarcaEAvisaNaProxima() // F13-13
    {
        var mensageiro = new Mensageiro(CanalOperacao.Alertas) { Falhar = true };
        var avisos = Avisos(mensageiro);

        (await avisos.FalhasAsync("diario", [new("expurgo-auditoria", 1)])).Should().Be(0);
        mensageiro.Falhar = false;
        (await avisos.FalhasAsync("diario", [new("expurgo-auditoria", 1)])).Should().Be(1);
    }

    [Fact]
    public async Task DiarioAsync_ComFalha_SoAlertasSemResumo() // F13-13, F13-14
    {
        var mensageiro = new Mensageiro(CanalOperacao.Alertas, CanalOperacao.Deploy);

        await Avisos(mensageiro).DiarioAsync([new("expurgo-auditoria", 1)], [("fechadas", 2)]);

        mensageiro.Enviadas.Should().ContainSingle().Which.Canal.Should().Be(CanalOperacao.Alertas);
    }

    [Fact]
    public async Task DiarioAsync_SemFalha_SoOResumo() // F13-14
    {
        var mensageiro = new Mensageiro(CanalOperacao.Alertas, CanalOperacao.Deploy);

        await Avisos(mensageiro).DiarioAsync([], [("fechadas", 2)]);

        mensageiro.Enviadas.Should().ContainSingle().Which.Canal.Should().Be(CanalOperacao.Deploy);
    }

    [Fact]
    public void PorPasso_LancouEFalhouPorTenant_SomaPorPassoSemZerados() =>
        AvisosDosJobs.PorPasso(
                ["expurgo-auditoria", "expurgo-cadastros"],
                [new("fechamento-automatico", 2), new("expurgo-cadastros", 0), new("recorrencias", 0)])
            .Should().BeEquivalentTo(new[]
            {
                new FalhaDePasso("expurgo-auditoria", 1),
                new FalhaDePasso("expurgo-cadastros", 1),
                new FalhaDePasso("fechamento-automatico", 2),
            });

    [Fact]
    public async Task FalhasAsync_SemWebhookDeAlertas_NadaSaiNemMarca()
    {
        var mensageiro = new Mensageiro(CanalOperacao.Deploy);

        (await Avisos(mensageiro).FalhasAsync("tick", [new("alertas-sla", 1)])).Should().Be(0);

        _marcas.Feitas.Should().BeEmpty();
    }

    [Fact]
    public async Task ResumoDoDiarioAsync_DisparoRepetidoNoDia_UmaMensagemEmDeploy() // F13-14
    {
        var mensageiro = new Mensageiro(CanalOperacao.Deploy);
        var avisos = Avisos(mensageiro);

        (await avisos.ResumoDoDiarioAsync([("fechadas", 3), ("auditoria", 0)])).Should().BeTrue();
        (await avisos.ResumoDoDiarioAsync([("fechadas", 0), ("auditoria", 0)])).Should().BeFalse("o Scheduler repetiu o disparo");

        mensageiro.Enviadas.Should().Equal((CanalOperacao.Deploy, "**Diário ok** · 10/11: fechadas 3, auditoria 0."));
    }
}
