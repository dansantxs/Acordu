using Acordu.Domain.Entities;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.UnitTests.Entities;

public sealed class AjusteSaldoTests
{
    private static readonly Guid Autor = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateOnly Dia10 = new(2026, 10, 10);

    private static AjusteSaldo Registrar(
        Contrato contrato,
        int minutos = 120,
        string motivo = "Recarga de 2h",
        DateOnly? data = null,
        Guid? autor = null) =>
        AjusteSaldo.Registrar(Guid.NewGuid(), contrato, new DuracaoAssinada(minutos), motivo, data ?? Dia10, autor ?? Autor);

    [Fact]
    public void Registrar_Recarga_HerdaTenantClienteEContrato()
    {
        var contrato = Contratos.Criar(TipoContrato.BancoHoras);

        var ajuste = Registrar(contrato);

        ajuste.TenantId.Should().Be(contrato.TenantId);
        ajuste.ClienteId.Should().Be(contrato.ClienteId);
        ajuste.ContratoId.Should().Be(contrato.Id);
        ajuste.Quantidade.Should().Be(new DuracaoAssinada(120));
        ajuste.Motivo.Should().Be("Recarga de 2h");
        ajuste.Data.Should().Be(Dia10);
        ajuste.AutorId.Should().Be(Autor);
    }

    [Fact]
    public void Registrar_Estorno_AceitaNegativo()
    {
        var ajuste = Registrar(Contratos.Criar(), minutos: -90, motivo: "Estorno de apontamento duplicado");

        ajuste.Quantidade.EhNegativa.Should().BeTrue();
    }

    [Fact]
    public void Registrar_Zero_LancaExcecao()
    {
        var acao = () => Registrar(Contratos.Criar(), minutos: 0);

        acao.Should().Throw<ArgumentException>().WithParameterName("quantidade");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Registrar_SemMotivo_LancaExcecao(string motivo)
    {
        var acao = () => Registrar(Contratos.Criar(), motivo: motivo);

        acao.Should().Throw<ArgumentException>().WithParameterName("motivo");
    }

    [Fact]
    public void Registrar_SemAutor_LancaExcecao()
    {
        var acao = () => Registrar(Contratos.Criar(), autor: Guid.Empty);

        acao.Should().Throw<ArgumentException>().WithParameterName("autorId");
    }

    [Theory]
    [InlineData(TipoContrato.MensalFixo)]
    [InlineData(TipoContrato.Avulso)]
    public void Registrar_ContratoQueNaoEhBancoDeHoras_LancaExcecao(TipoContrato tipo)
    {
        var acao = () => Registrar(Contratos.Criar(tipo));

        acao.Should().Throw<ContratoIncoerenteException>();
    }

    [Fact]
    public void Registrar_ContratoEncerrado_LancaExcecao()
    {
        var contrato = Contratos.Criar();
        contrato.Encerrar(new DateOnly(2026, 10, 31));

        var acao = () => Registrar(contrato);

        acao.Should().Throw<ContratoIncoerenteException>();
    }

    [Fact]
    public void Registrar_ContratoSuspenso_Aceita()
    {
        var contrato = Contratos.Criar();
        contrato.Suspender();

        var acao = () => Registrar(contrato);

        acao.Should().NotThrow();
    }

    [Fact]
    public void Registrar_DataAntesDoInicioDoContrato_LancaExcecao()
    {
        var acao = () => Registrar(Contratos.Criar(), data: Contratos.InicioOutubro.AddDays(-1));

        acao.Should().Throw<ContratoIncoerenteException>();
    }
}
