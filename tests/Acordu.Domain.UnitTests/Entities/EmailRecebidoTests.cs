using Acordu.Domain.Entities;
using Acordu.Domain.Enums;

namespace Acordu.Domain.UnitTests.Entities;

/// <summary>Design do F15, D6 (ADR 0016, item 8): o registro de entrada guarda só hashes, o instante e o resultado.</summary>
public sealed class EmailRecebidoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 6, 10, 0, 0, TimeSpan.FromHours(-3));
    private static readonly string Chave = new('a', EmailRecebido.TamanhoHash);
    private static readonly string Remetente = new('b', EmailRecebido.TamanhoHash);

    [Fact]
    public void Registrar_Valido_GuardaHashesInstanteEmUtcEResultado()
    {
        var tenant = Guid.NewGuid();

        var registro = EmailRecebido.Registrar(Guid.NewGuid(), tenant, Chave, Remetente, ResultadoEntradaEmail.Ignorado, Agora);

        registro.TenantId.Should().Be(tenant);
        registro.ChaveEntrada.Should().Be(Chave);
        registro.RemetenteHash.Should().Be(Remetente);
        registro.Resultado.Should().Be(ResultadoEntradaEmail.Ignorado);
        registro.RecebidoEm.Should().Be(Agora);
        registro.RecebidoEm.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Registrar_SemRemetente_RemetenteNulo()
    {
        EmailRecebido.Registrar(Guid.NewGuid(), Guid.NewGuid(), Chave, null, ResultadoEntradaEmail.Ignorado, Agora)
            .RemetenteHash.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    [InlineData("ana@cliente.com.br")]
    public void Registrar_ChaveForaDoFormato_ArgumentException(string chave)
    {
        var registrar = () => EmailRecebido.Registrar(Guid.NewGuid(), Guid.NewGuid(), chave, null, ResultadoEntradaEmail.Ignorado, Agora);

        registrar.Should().Throw<ArgumentException>().WithParameterName("chaveEntrada");
    }

    [Fact]
    public void Registrar_RemetenteEmClaro_ArgumentException()
    {
        var registrar = () => EmailRecebido.Registrar(
            Guid.NewGuid(), Guid.NewGuid(), Chave, "ana@cliente.com.br", ResultadoEntradaEmail.Ignorado, Agora);

        registrar.Should().Throw<ArgumentException>().WithParameterName("remetenteHash");
    }

    [Fact]
    public void Registrar_ResultadoDesconhecido_ArgumentOutOfRange()
    {
        var registrar = () => EmailRecebido.Registrar(Guid.NewGuid(), Guid.NewGuid(), Chave, null, (ResultadoEntradaEmail)99, Agora);

        registrar.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("resultado");
    }

    [Fact]
    public void Registrar_TenantVazio_ArgumentException()
    {
        var registrar = () => EmailRecebido.Registrar(Guid.NewGuid(), Guid.Empty, Chave, null, ResultadoEntradaEmail.Ignorado, Agora);

        registrar.Should().Throw<ArgumentException>();
    }
}
