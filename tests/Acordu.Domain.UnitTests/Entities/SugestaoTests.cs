using Acordu.Domain.Entities;

namespace Acordu.Domain.UnitTests.Entities;

/// <summary>Spec do F13, F13-24: o texto da sugestão tem de 10 a 2.000 caracteres.</summary>
public sealed class SugestaoTests
{
    private const string Campo = "texto";

    private static readonly DateTimeOffset Agora = new(2026, 11, 10, 13, 0, 0, TimeSpan.Zero);

    private static Sugestao Criar(string texto, string? tela = null) =>
        Sugestao.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), texto, tela, Agora);

    [Theory]
    [InlineData("         ")]
    [InlineData("curto")]
    [InlineData("  nove car  ")]
    public void Criar_TextoCurto_Recusa(string curto)
    {
        var acao = () => Criar(curto);

        acao.Should().Throw<ArgumentException>().WithParameterName(Campo);
    }

    [Fact]
    public void Criar_TextoLongo_Recusa()
    {
        var acao = () => Criar(new string('a', Sugestao.TextoMaximo + 1));

        acao.Should().Throw<ArgumentException>().WithParameterName(Campo);
    }

    [Fact]
    public void Criar_NoLimite_GuardaSemEspacosEPendente()
    {
        var sugestao = Criar("  " + new string('a', Sugestao.TextoMaximo) + "  ", "  /tarefas ");

        sugestao.Texto.Should().HaveLength(Sugestao.TextoMaximo);
        sugestao.Tela.Should().Be("/tarefas");
        sugestao.AvisadaEm.Should().BeNull();
        sugestao.CriadaEm.Should().Be(Agora);
    }}
