using System.Text;
using Acordu.Application.Anexos;

namespace Acordu.Application.UnitTests.Anexos;

public sealed class TiposDeArquivoTests
{
    private static string? Detectar(byte[] conteudo, string nome) => TiposDeArquivo.Detectar(conteudo, nome, conteudo.Length);

    [Fact]
    public void Imagens_PelaAssinatura_IgnorandoAExtensao()
    {
        Detectar([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0], "foto.jpg").Should().Be(TiposDeArquivo.Png);
        Detectar([0xFF, 0xD8, 0xFF, 0xE0, 0], "foto.png").Should().Be(TiposDeArquivo.Jpeg);
        Detectar(Encoding.ASCII.GetBytes("GIF89a....."), "a.gif").Should().Be(TiposDeArquivo.Gif);
        Detectar(Encoding.ASCII.GetBytes("RIFF\0\0\0\0WEBPVP8 "), "a.webp").Should().Be(TiposDeArquivo.Webp);
    }

    [Fact]
    public void Pdf_ZipEOffice()
    {
        byte[] zip = [0x50, 0x4B, 0x03, 0x04, 0x14, 0];
        byte[] ole = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0];

        Detectar(Encoding.ASCII.GetBytes("%PDF-1.7\n"), "nota.pdf").Should().Be(TiposDeArquivo.Pdf);
        Detectar(zip, "arquivos.zip").Should().Be(TiposDeArquivo.Zip);
        Detectar(zip, "Contrato.DOCX").Should().Be(TiposDeArquivo.Docx);
        Detectar(zip, "planilha.xlsx").Should().Be(TiposDeArquivo.Xlsx);
        Detectar(ole, "antigo.xls").Should().Be(TiposDeArquivo.Xls);
        Detectar(ole, "sem-extensao").Should().BeNull();
    }

    [Fact]
    public void Texto_Utf8SemNulos_CsvPelaExtensao()
    {
        var texto = Encoding.UTF8.GetBytes("nome;valor\nAção;10\n");

        Detectar(texto, "dados.csv").Should().Be(TiposDeArquivo.Csv);
        Detectar(texto, "leia.txt").Should().Be(TiposDeArquivo.Texto);
        Detectar([0x41, 0x00, 0x42], "binario.txt").Should().BeNull();
        Detectar([0xC3, 0x28], "invalido.txt").Should().BeNull();
    }

    [Fact]
    public void TextoCortadoNoMeioDeUmCaractere_AindaETexto()
    {
        var texto = Encoding.UTF8.GetBytes("ção");

        TiposDeArquivo.Detectar(texto.AsSpan(0, 3), "a.txt", texto.Length).Should().Be(TiposDeArquivo.Texto);
    }

    [Fact]
    public void Executavel_NaoEDeTipoConhecido()
    {
        Detectar([0x4D, 0x5A, 0x90, 0x00, 0x03], "programa.pdf").Should().BeNull();
    }
}
