using Acordu.Domain.Entities;
using Acordu.Domain.Enums;

namespace Acordu.Domain.UnitTests.Entities;

/// <summary>Spec do F14, F14-81 e F14-83: o registro da versão aceita de um texto legal (design D10).</summary>
public sealed class AceiteTextoTests
{
    private static readonly Guid Conta = Guid.CreateVersion7();

    [Fact] // F14-81
    public void Registrar_Aceite_GuardaContaTextoVersaoFormaEDataEmUtc()
    {
        var aceite = AceiteTexto.Registrar(
            Conta, TipoTextoLegal.Termos, " 1 ", FormaAceiteTexto.Aceite, new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.FromHours(-3)));

        aceite.UsuarioId.Should().Be(Conta);
        aceite.Texto.Should().Be(TipoTextoLegal.Termos);
        aceite.Versao.Should().Be("1");
        aceite.Forma.Should().Be(FormaAceiteTexto.Aceite);
        aceite.RegistradoEm.Should().Be(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero));
        aceite.RegistradoEm.Offset.Should().Be(TimeSpan.Zero);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("123456789012345678901")]
    public void Registrar_VersaoVaziaOuLonga_LancaExcecao(string? valor)
    {
        var acao = () => AceiteTexto.Registrar(Conta, TipoTextoLegal.Privacidade, valor!, FormaAceiteTexto.Ciente, DateTimeOffset.UnixEpoch);

        acao.Should().Throw<ArgumentException>().WithParameterName("versao");
    }

    [Fact]
    public void Registrar_SemConta_LancaExcecao()
    {
        var acao = () => AceiteTexto.Registrar(Guid.Empty, TipoTextoLegal.Termos, "1", FormaAceiteTexto.Aceite, DateTimeOffset.UnixEpoch);

        acao.Should().Throw<ArgumentException>().WithParameterName("usuarioId");
    }

    [Fact]
    public void Registrar_TextoDesconhecido_LancaExcecao()
    {
        var acao = () => AceiteTexto.Registrar(Conta, (TipoTextoLegal)99, "1", FormaAceiteTexto.Aceite, DateTimeOffset.UnixEpoch);

        acao.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("texto");
    }
}
