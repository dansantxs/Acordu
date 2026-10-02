using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.UnitTests.ValueObjects;

public sealed class DuracaoAssinadaTests
{
    [Fact]
    public void AceitaNegativo()
    {
        var estorno = new DuracaoAssinada(-120);

        estorno.EhNegativa.Should().BeTrue();
    }

    [Fact]
    public void ConversaoDeDuracao_PreservaMinutos()
    {
        DuracaoAssinada assinada = Duracao.FromMinutos(90);

        assinada.Minutos.Should().Be(90);
    }

    [Fact]
    public void Aritmetica_PodeFicarNegativa()
    {
        var saldo = new DuracaoAssinada(60) - new DuracaoAssinada(90);

        saldo.Should().Be(new DuracaoAssinada(-30));
        (-saldo).Should().Be(new DuracaoAssinada(30));
    }
}
