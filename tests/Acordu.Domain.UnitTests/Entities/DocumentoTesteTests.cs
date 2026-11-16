using Acordu.Domain.Entities;
using Acordu.Domain.Exceptions;

namespace Acordu.Domain.UnitTests.Entities;

/// <summary>Spec do F14, F14-57 e F14-59: o registro global de que um documento já teve teste (design D8).</summary>
public sealed class DocumentoTesteTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly string Hash = new('a', DocumentoTeste.TamanhoHash);

    [Fact] // F14-57
    public void Registrar_GuardaOHashEOPrimeiroTesteSemLiberacao()
    {
        var registro = DocumentoTeste.Registrar(Hash, new DateTimeOffset(2026, 11, 16, 9, 0, 0, TimeSpan.FromHours(-3)));

        registro.Hash.Should().Be(Hash);
        registro.PrimeiroTesteEm.Should().Be(Agora);
        registro.PrimeiroTesteEm.Offset.Should().Be(TimeSpan.Zero);
        registro.LiberadoEm.Should().BeNull();
        registro.LiberacaoUsadaEm.Should().BeNull();
        registro.LiberacaoDisponivel.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("52998224725")]
    public void Registrar_HashForaDoFormato_LancaExcecao(string valor)
    {
        // Nunca o documento em claro, nem um hash truncado (D8).
        var acao = () => DocumentoTeste.Registrar(valor, Agora);

        acao.Should().Throw<ArgumentException>().WithParameterName("hash");
    }

    [Fact]
    public void Registrar_HashComMaiusculas_LancaExcecao()
    {
        var acao = () => DocumentoTeste.Registrar(new string('A', DocumentoTeste.TamanhoHash), Agora);

        acao.Should().Throw<ArgumentException>().WithParameterName("hash");
    }

    [Fact] // F14-57, F14-59
    public void UsarLiberacao_SemLiberacao_LancaTransicaoInvalida()
    {
        var registro = DocumentoTeste.Registrar(Hash, Agora);

        var acao = () => registro.UsarLiberacao(Agora.AddDays(1));

        acao.Should().Throw<TransicaoInvalidaException>();
        registro.LiberacaoUsadaEm.Should().BeNull();
    }
}
