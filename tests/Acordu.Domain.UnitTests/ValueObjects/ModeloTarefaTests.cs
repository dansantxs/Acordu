using Acordu.Domain.Entities;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.UnitTests.ValueObjects;

public sealed class ModeloTarefaTests
{
    [Fact]
    public void Criar_AparaTextosDaChecklist()
    {
        var modelo = new ModeloTarefa("Fechamento mensal", null, null, [" Conciliar extratos ", "Emitir guias"], null, true);

        modelo.Checklist.Should().Equal("Conciliar extratos", "Emitir guias");
    }

    [Fact]
    public void Criar_ItemVazio_LancaExcecao()
    {
        var acao = () => new ModeloTarefa("Fechamento", null, null, ["ok", " "], null, false);

        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Criar_ChecklistAlemDoLimite_LancaExcecao()
    {
        var itens = Enumerable.Range(0, Tarefa.MaximoItensChecklist + 1).Select(i => $"Item {i}");

        var acao = () => new ModeloTarefa("Fechamento", null, null, itens, null, false);

        acao.Should().Throw<ArgumentException>().WithParameterName("checklist");
    }

    [Fact]
    public void Criar_EstimativaZero_LancaExcecao()
    {
        var acao = () => new ModeloTarefa("Fechamento", null, null, [], Duracao.Zero, false);

        acao.Should().Throw<ArgumentException>().WithParameterName("estimativa");
    }

    [Fact]
    public void Igualdade_ComparaAChecklist()
    {
        var a = new ModeloTarefa("F", null, null, ["um", "dois"], Duracao.FromHoras(4), true);
        var b = new ModeloTarefa("F", null, null, ["um", "dois"], Duracao.FromHoras(4), true);
        var c = new ModeloTarefa("F", null, null, ["dois", "um"], Duracao.FromHoras(4), true);

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
        a.Should().NotBe(c);
    }
}
