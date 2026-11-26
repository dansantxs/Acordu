using Acordu.Application.EntradaEmail;
using Acordu.Domain.Entities;

namespace Acordu.Application.UnitTests.EntradaEmail;

/// <summary>Design do F15, D6 (ADR 0016, item 8): a chave de idempotência e o hash do remetente, sem nada em claro.</summary>
public sealed class ChaveDeEntradaTests
{
    private const string Destino = "k7m2qd9w@entrada.acordu.app.br";
    private const string Conteudo = "0f343b0931126a20f133d67c2b018a3b5c2d4a9a3b9b5a8b9e8c2f1c1d2e3f40";

    [Fact]
    public void Calcular_MesmoMessageIdEDestino_MesmaChave()
    {
        var primeira = ChaveDeEntrada.Calcular("<abc@mail.exemplo.com>", Destino, Conteudo);
        var repetida = ChaveDeEntrada.Calcular(" abc@mail.exemplo.com ", Destino.ToUpperInvariant(), new string('1', 64));

        repetida.Should().Be(primeira, "a repetição da entrega pode vir com outro corpo (cabeçalhos do servidor), o Message-ID é o mesmo");
        EmailRecebido.HashValido(primeira).Should().BeTrue();
        primeira.Should().NotContain("abc");
    }

    [Fact]
    public void Calcular_OutroDestino_OutraChave() // F15-21: o mesmo e-mail para duas empresas conta em cada uma
    {
        var a = ChaveDeEntrada.Calcular("<abc@mail.exemplo.com>", Destino, Conteudo);
        var b = ChaveDeEntrada.Calcular("<abc@mail.exemplo.com>", "x2m4p6r8@entrada.acordu.app.br", Conteudo);

        b.Should().NotBe(a);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" <> ")]
    public void Calcular_SemMessageId_PeloConteudo(string? messageId)
    {
        var chave = ChaveDeEntrada.Calcular(messageId, Destino, Conteudo);

        chave.Should().Be(ChaveDeEntrada.Calcular(null, Destino, Conteudo));
        chave.Should().NotBe(ChaveDeEntrada.Calcular(null, Destino, new string('2', 64)), "outro conteúdo é outro e-mail");
    }

    [Fact]
    public void Calcular_MessageIdIgualAoHashDoConteudo_NaoConfunde()
    {
        ChaveDeEntrada.Calcular(Conteudo, Destino, Conteudo).Should().NotBe(ChaveDeEntrada.Calcular(null, Destino, Conteudo));
    }

    [Fact]
    public void DoRemetente_UmEndereco_HashDoEmailNormalizado()
    {
        var hash = ChaveDeEntrada.DoRemetente(["Ana@Cliente.com.br"]);

        hash.Should().Be(ChaveDeEntrada.DoRemetente([" ana@cliente.com.br "]));
        EmailRecebido.HashValido(hash).Should().BeTrue();
    }

    [Fact]
    public void DoRemetente_NenhumVariosOuInvalido_Nulo()
    {
        ChaveDeEntrada.DoRemetente([]).Should().BeNull();
        ChaveDeEntrada.DoRemetente(["ana@cliente.com.br", "bruno@cliente.com.br"]).Should().BeNull();
        ChaveDeEntrada.DoRemetente(["sem-arroba"]).Should().BeNull();
    }
}
