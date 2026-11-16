using System.Security.Cryptography;
using System.Text;
using Acordu.Application.Cadastro;

namespace Acordu.Application.UnitTests.Cadastro;

/// <summary>Design do F14, D8 (F14-57, F14-58): o registro do documento guarda o HMAC-SHA256 dele, nunca o documento.</summary>
public sealed class ProtecaoDocumentoTests
{
    private const string Chave = "chave-dos-testes-de-unidade-com-mais-de-32-bytes";
    private static readonly ProtecaoDocumento Protecao = ProtecaoDocumento.DaConfiguracao(Chave);

    [Fact]
    public void Hash_HmacSha256DoDocumentoEmHexMinusculo()
    {
        var esperado = Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(Chave), Encoding.UTF8.GetBytes("52998224725")));

        var hash = Protecao.Hash("52998224725");

        hash.Should().Be(esperado).And.HaveLength(64).And.MatchRegex("^[0-9a-f]{64}$");
        hash.Should().NotContain("52998224725");
    }

    [Fact] // F14-58: o mesmo documento normalizado dá o mesmo hash
    public void Hash_MesmoDocumento_MesmoHash()
    {
        Protecao.Hash("12ABC34501DE35").Should().Be(Protecao.Hash("12ABC34501DE35"));
        Protecao.Hash("12ABC34501DE35").Should().NotBe(Protecao.Hash("11222333000181"));
    }

    [Fact]
    public void Hash_OutraChave_OutroHash()
    {
        // Sem a chave, o hash não se desfaz por força bruta (D8); trocar a chave muda todos os hashes.
        var outra = ProtecaoDocumento.DaConfiguracao("outra-chave-dos-testes-com-mais-de-32-bytes");

        outra.Hash("52998224725").Should().NotBe(Protecao.Hash("52998224725"));
        Protecao.Hash("52998224725").Should().NotBe(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("52998224725"))));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("curta-demais")]
    [InlineData("1234567890123456789012345678901")] // 31 bytes
    public void DaConfiguracao_ChaveAusenteOuCurta_ImpedeASubida(string? chave)
    {
        var acao = () => ProtecaoDocumento.DaConfiguracao(chave);

        acao.Should().Throw<InvalidOperationException>().WithMessage("*Documentos__ChaveHmac*");
    }

    [Fact]
    public void DaConfiguracao_Chave32Bytes_Aceita()
    {
        ProtecaoDocumento.DaConfiguracao(new string('x', 32)).Hash("52998224725").Should().HaveLength(64);
    }
}
