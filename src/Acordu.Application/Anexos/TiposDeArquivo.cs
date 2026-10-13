using System.Text.Unicode;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Anexos;

/// <summary>
/// Tipo do arquivo pelo conteúdo (<i>magic bytes</i>), não pelo que o navegador declara (segurança §7). A extensão só
/// desempata o que tem a mesma assinatura: ZIP × documentos Office (OOXML são ZIP), OLE × Office antigo e texto × CSV.
/// </summary>
public static class TiposDeArquivo
{
    /// <summary>Quantos bytes do início bastam para detectar (o texto é conferido nesse trecho).</summary>
    public const int BytesParaDetectar = 8192;

    public const string Png = "image/png";
    public const string Jpeg = "image/jpeg";
    public const string Gif = "image/gif";
    public const string Webp = "image/webp";
    public const string Pdf = "application/pdf";
    public const string Zip = "application/zip";
    public const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string Pptx = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
    public const string Doc = "application/msword";
    public const string Xls = "application/vnd.ms-excel";
    public const string Ppt = "application/vnd.ms-powerpoint";
    public const string Texto = "text/plain";
    public const string Csv = "text/csv";

    /// <summary>Política padrão (contrato §7): 5 anexos de até 20 MB, dos tipos acima.</summary>
    public static PoliticaAnexos PoliticaPadrao { get; } = new(
        20L * 1024 * 1024,
        [Png, Jpeg, Gif, Webp, Pdf, Zip, Docx, Xlsx, Pptx, Doc, Xls, Ppt, Texto, Csv],
        5);

    private static readonly byte[] AssinaturaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] AssinaturaJpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] AssinaturaPdf = "%PDF-"u8.ToArray();
    private static readonly byte[] AssinaturaZip = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] AssinaturaOle = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    /// <summary>Content type detectado, ou nulo se o conteúdo não é de um tipo conhecido.</summary>
    /// <param name="inicio">Os primeiros bytes do arquivo (até <see cref="BytesParaDetectar"/>).</param>
    /// <param name="tamanhoTotal">Tamanho do arquivo inteiro: diz se <paramref name="inicio"/> foi cortado.</param>
    public static string? Detectar(ReadOnlySpan<byte> inicio, string nomeArquivo, long tamanhoTotal)
    {
        ArgumentNullException.ThrowIfNull(nomeArquivo);
        var extensao = Path.GetExtension(nomeArquivo).ToLowerInvariant();
        if (inicio.StartsWith(AssinaturaPng))
            return Png;
        if (inicio.StartsWith(AssinaturaJpeg))
            return Jpeg;
        if (inicio.StartsWith("GIF87a"u8) || inicio.StartsWith("GIF89a"u8))
            return Gif;
        if (inicio.Length >= 12 && inicio.StartsWith("RIFF"u8) && inicio[8..12].SequenceEqual("WEBP"u8))
            return Webp;
        if (inicio.StartsWith(AssinaturaPdf))
            return Pdf;
        if (inicio.StartsWith(AssinaturaZip))
        {
            return extensao switch
            {
                ".docx" => Docx,
                ".xlsx" => Xlsx,
                ".pptx" => Pptx,
                _ => Zip,
            };
        }

        if (inicio.StartsWith(AssinaturaOle))
        {
            return extensao switch
            {
                ".doc" => Doc,
                ".xls" => Xls,
                ".ppt" => Ppt,
                _ => null,
            };
        }

        return EhTexto(inicio, inicio.Length < tamanhoTotal) ? (extensao == ".csv" ? Csv : Texto) : null;
    }

    /// <summary>UTF-8 válido e sem bytes nulos. Trecho cortado pode terminar no meio de um caractere: até 3 bytes de folga.</summary>
    private static bool EhTexto(ReadOnlySpan<byte> inicio, bool cortado)
    {
        if (inicio.IsEmpty || inicio.Contains((byte)0))
            return false;
        if (Utf8.IsValid(inicio))
            return true;
        if (!cortado)
            return false;
        for (var corte = 1; corte <= 3 && corte < inicio.Length; corte++)
        {
            if (Utf8.IsValid(inicio[..^corte]))
                return true;
        }

        return false;
    }
}
