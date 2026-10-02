using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.UnitTests.ValueObjects;

public sealed class PoliticaSlaTests
{
    private static PrazoSla Prazo(Prioridade prioridade, int horasResolucao = 8) =>
        new(prioridade, Duracao.FromHoras(1), Duracao.FromHoras(horasResolucao));

    [Fact]
    public void Criar_UmPrazoPorPrioridade_OrdenaPorPrioridade()
    {
        var politica = new PoliticaSla(
            [Prazo(Prioridade.Urgente), Prazo(Prioridade.Baixa), Prazo(Prioridade.Alta), Prazo(Prioridade.Media)]);

        politica.Prazos.Select(p => p.Prioridade).Should().Equal(
            Prioridade.Baixa, Prioridade.Media, Prioridade.Alta, Prioridade.Urgente);
    }

    [Fact]
    public void Criar_FaltandoPrioridade_LancaExcecao()
    {
        var acao = () => new PoliticaSla([Prazo(Prioridade.Baixa), Prazo(Prioridade.Media), Prazo(Prioridade.Alta)]);

        acao.Should().Throw<SlaIncompletoException>().WithMessage("*Urgente*");
    }

    [Fact]
    public void Criar_PrioridadeDuplicada_LancaExcecao()
    {
        var acao = () => new PoliticaSla(
        [
            Prazo(Prioridade.Baixa), Prazo(Prioridade.Media), Prazo(Prioridade.Alta),
            Prazo(Prioridade.Urgente), Prazo(Prioridade.Alta, 10),
        ]);

        acao.Should().Throw<SlaIncompletoException>().WithMessage("*Alta*");
    }

    [Fact]
    public void Criar_Vazia_LancaExcecao()
    {
        var acao = () => new PoliticaSla([]);

        acao.Should().Throw<SlaIncompletoException>();
    }

    [Fact]
    public void PrazosDe_Prioridade_DevolveOPrazoDela()
    {
        var politica = Contratos.PoliticaPadrao();

        politica.PrazosDe(Prioridade.Urgente).Resolucao.Should().Be(Duracao.FromHoras(4));
        politica.PrazosDe(Prioridade.Baixa).PrimeiraResposta.Should().Be(Duracao.FromHoras(8));
    }

    [Fact]
    public void Igualdade_MesmosPrazosEmOutraOrdem_SaoIguais()
    {
        var a = new PoliticaSla([Prazo(Prioridade.Baixa), Prazo(Prioridade.Media), Prazo(Prioridade.Alta), Prazo(Prioridade.Urgente)]);
        var b = new PoliticaSla([Prazo(Prioridade.Urgente), Prazo(Prioridade.Alta), Prazo(Prioridade.Media), Prazo(Prioridade.Baixa)]);

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Igualdade_UmPrazoDiferente_SaoDiferentes()
    {
        var a = new PoliticaSla([Prazo(Prioridade.Baixa), Prazo(Prioridade.Media), Prazo(Prioridade.Alta), Prazo(Prioridade.Urgente)]);
        var b = new PoliticaSla([Prazo(Prioridade.Baixa), Prazo(Prioridade.Media), Prazo(Prioridade.Alta), Prazo(Prioridade.Urgente, 9)]);

        a.Should().NotBe(b);
    }
}
