using Acordu.Application.Sugestoes;

namespace Acordu.Application.UnitTests.Sugestoes;

/// <summary>Spec do F13, F13-28: a mensagem de #sugestoes cabe numa mensagem do Discord.</summary>
public sealed class ComandosSugestaoTests
{
    [Fact]
    public void Texto_SugestaoCurta_CabecalhoETextoInteiro() =>
        ComandosSugestao.Texto(new SugestaoParaAviso(Guid.NewGuid(), 7, "Filtrar por cliente.", "/tarefas", "Estúdio Traço"))
            .Should().Be("**Sugestão #7** · Estúdio Traço · tela `/tarefas`\nFiltrar por cliente.");

    [Fact]
    public void Texto_SugestaoLonga_CortaComANotaDaIntegra()
    {
        var texto = ComandosSugestao.Texto(new SugestaoParaAviso(Guid.NewGuid(), 7, new string('a', 2000), null, "Estúdio Traço"));

        texto.Length.Should().BeLessThanOrEqualTo(2000);
        texto.Should().StartWith("**Sugestão #7** · Estúdio Traço\n").And.EndWith("… (íntegra no banco, #7)");
    }
}
