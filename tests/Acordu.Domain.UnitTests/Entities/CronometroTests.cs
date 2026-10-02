using Acordu.Domain.Entities;
using Acordu.Domain.ValueObjects;
using static Acordu.Domain.UnitTests.Calendarios;

namespace Acordu.Domain.UnitTests.Entities;

public sealed class CronometroTests
{
    private static Cronometro IniciadoAsDez() =>
        Cronometro.Iniciar(Guid.NewGuid(), Tenant, Guid.NewGuid(), Guid.NewGuid(), Brasilia(6, 10));

    [Fact]
    public void Iniciar_GuardaInstanteEmUtc()
    {
        var cronometro = IniciadoAsDez();

        cronometro.IniciadoEm.Should().Be(Brasilia(6, 10));
        cronometro.IniciadoEm.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Iniciar_SemTarefa_LancaExcecao()
    {
        var acao = () => Cronometro.Iniciar(Guid.NewGuid(), Tenant, Guid.NewGuid(), Guid.Empty, Brasilia(6, 10));

        acao.Should().Throw<ArgumentException>().WithParameterName("tarefaId");
    }

    [Fact]
    public void Parar_MinutosExatos()
    {
        IniciadoAsDez().Parar(Brasilia(6, 11, 30)).Should().Be(Duracao.FromMinutos(90));
    }

    [Theory]
    [InlineData(29, 0)]
    [InlineData(30, 1)]
    [InlineData(59, 1)]
    public void Parar_ArredondaAoMinutoMaisProximo(int segundos, int minutosAMais)
    {
        var parada = Brasilia(6, 10, 10).AddSeconds(segundos);

        IniciadoAsDez().Parar(parada).Should().Be(Duracao.FromMinutos(10 + minutosAMais));
    }

    [Fact]
    public void Parar_MenosDeMeioMinuto_ContaUmMinuto()
    {
        IniciadoAsDez().Parar(Brasilia(6, 10).AddSeconds(10)).Should().Be(Duracao.FromMinutos(1));
    }

    [Fact]
    public void Parar_AcimaDe24h_NaoCorta()
    {
        IniciadoAsDez().Parar(Brasilia(8, 10)).Should().Be(Duracao.FromHoras(48));
    }

    [Fact]
    public void Parar_AntesDoInicio_LancaExcecao()
    {
        var acao = () => IniciadoAsDez().Parar(Brasilia(6, 9));

        acao.Should().Throw<ArgumentException>();
    }
}
