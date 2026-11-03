using Acordu.Api.Jobs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acordu.Api.FunctionalTests;

/// <summary>Spec do F8, cenário F8-08: os passos do job diário são independentes.</summary>
public sealed class PassosDiarioTests
{
    [Fact]
    public async Task ExecutarAsync_UmPassoFalha_OsSeguintesRodamEAFalhaConta()
    {
        var rodados = new List<string>();

        var resultado = await PassosDiario.ExecutarAsync(
            [
                new("primeiro", _ => { rodados.Add("primeiro"); return Task.FromResult(3); }),
                new("quebra", _ => throw new InvalidOperationException("banco fora")),
                new("ultimo", _ => { rodados.Add("ultimo"); return Task.FromResult(5); }),
            ],
            NullLogger.Instance,
            CancellationToken.None);

        rodados.Should().Equal("primeiro", "ultimo");
        resultado.Contagens.Should().Equal(3, 0, 5);
        resultado.Falhas.Should().Be(1);
    }

    [Fact]
    public async Task ExecutarAsync_CanceladoPorOutraCoisa_ContaComoFalhaESegue()
    {
        var resultado = await PassosDiario.ExecutarAsync(
            [
                new("timeout", _ => throw new OperationCanceledException("tempo do comando esgotado")),
                new("seguinte", _ => Task.FromResult(2)),
            ],
            NullLogger.Instance,
            CancellationToken.None);

        resultado.Contagens.Should().Equal(0, 2);
        resultado.Falhas.Should().Be(1);
    }
}
