using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.UnitTests.ValueObjects;

public sealed class PrazoSlaTests
{
    [Fact]
    public void Criar_PrazosValidos_GuardaValores()
    {
        var prazo = new PrazoSla(Prioridade.Alta, Duracao.FromHoras(2), Duracao.FromHoras(16));

        prazo.Prioridade.Should().Be(Prioridade.Alta);
        prazo.PrimeiraResposta.Should().Be(Duracao.FromHoras(2));
        prazo.Resolucao.Should().Be(Duracao.FromHoras(16));
    }

    [Fact]
    public void Criar_ResolucaoIgualAPrimeiraResposta_Aceita()
    {
        var acao = () => new PrazoSla(Prioridade.Baixa, Duracao.FromHoras(4), Duracao.FromHoras(4));

        acao.Should().NotThrow();
    }

    [Fact]
    public void Criar_ResolucaoMenorQuePrimeiraResposta_LancaExcecao()
    {
        var acao = () => new PrazoSla(Prioridade.Baixa, Duracao.FromHoras(4), Duracao.FromHoras(3));

        acao.Should().Throw<SlaIncompletoException>();
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(60, 0)]
    public void Criar_PrazoZero_LancaExcecao(int primeiraResposta, int resolucao)
    {
        var acao = () => new PrazoSla(Prioridade.Media, Duracao.FromMinutos(primeiraResposta), Duracao.FromMinutos(resolucao));

        acao.Should().Throw<SlaIncompletoException>();
    }

    [Fact]
    public void Criar_PrioridadeInvalida_LancaExcecao()
    {
        var acao = () => new PrazoSla((Prioridade)99, Duracao.FromHoras(1), Duracao.FromHoras(2));

        acao.Should().Throw<ArgumentOutOfRangeException>();
    }
}
