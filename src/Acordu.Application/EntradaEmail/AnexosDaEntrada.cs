using System.Globalization;
using System.Text;
using Acordu.Application.Anexos;
using Acordu.Domain.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.EntradaEmail;

/// <summary>
/// Os anexos de um e-mail recebido pela mesma política do portal (RNF-SEG-07; design do F15, D14; segurança §7), mas
/// <b>um por um</b>: o portal recusa o envio inteiro, e o e-mail não tem como ser refeito, então o que está fora vira a
/// lista dos recusados, com o motivo, e o resto entra (pergunta 4, F15-14, F15-15). Funções puras: quem chama sobe os
/// válidos ao storage e grava a nota do sistema e o registro de entrada.
/// <para>
/// Cada parte, na ordem do MIME: o tipo declarado que nunca entra (mensagem anexada, parte cifrada ou assinada) é "tipo não
/// permitido"; depois o tamanho (acima de 20 MB); depois o tipo detectado pelo conteúdo (vazio ou fora da lista é "tipo não
/// permitido"); e, válido, entra até o 5º. Do 6º válido em diante, "além do limite". A imagem embutida conta como anexo.
/// </para>
/// </summary>
public static class AnexosDaEntrada
{
    /// <summary>Quantos recusados a nota e o aviso listam; os outros entram na contagem do fim.</summary>
    public const int RecusadosListados = 20;

    /// <summary>Tipos declarados que nunca entram, qualquer que seja o conteúdo (D14).</summary>
    private static readonly string[] PrefixosRecusados =
        ["message/", "multipart/encrypted", "application/pgp", "application/pkcs7", "application/x-pkcs7"];

    /// <summary>Extensão para a parte sem nome (a imagem embutida costuma vir só com o <c>Content-Id</c>).</summary>
    private static readonly Dictionary<string, string> Extensoes = new(StringComparer.OrdinalIgnoreCase)
    {
        [TiposDeArquivo.Png] = ".png",
        [TiposDeArquivo.Jpeg] = ".jpg",
        [TiposDeArquivo.Gif] = ".gif",
        [TiposDeArquivo.Webp] = ".webp",
        [TiposDeArquivo.Pdf] = ".pdf",
        ["message/rfc822"] = ".eml",
    };

    /// <summary>Separa os válidos (prontos para o <see cref="RecebimentoAnexos"/>) dos recusados.</summary>
    public static SeparacaoDosAnexos Separar(IReadOnlyList<AnexoDeEntrada>? anexos, PoliticaAnexos politica)
    {
        ArgumentNullException.ThrowIfNull(politica);
        if (anexos is null || anexos.Count == 0)
            return new SeparacaoDosAnexos([], null);

        var validos = new List<ArquivoRecebido>();
        var recusados = new List<AnexoRecusado>();
        for (var i = 0; i < anexos.Count; i++)
        {
            var anexo = anexos[i];
            var nome = NomeDoArquivo(anexo.NomeArquivo, i + 1, anexo.TipoDeclarado);
            if (Motivo(anexo, nome, politica) is { } motivo)
                recusados.Add(new AnexoRecusado(nome, motivo));
            else if (validos.Count >= politica.MaximoPorInteracao)
                recusados.Add(new AnexoRecusado(nome, MotivoRecusaAnexo.AlemDoLimite));
            else
                validos.Add(new ArquivoRecebido(nome, anexo.TamanhoBytes, anexo.Abrir));
        }

        return new SeparacaoDosAnexos(validos, recusados.Count == 0 ? null : new AnexosRecusados(recusados));
    }

    /// <summary>
    /// O nome que o anexo terá: o último trecho depois de <c>/</c> ou <c>\</c>, sem caracteres de controle, aparado e
    /// cortado em 255 mantendo a extensão; vazio, <c>.</c> ou <c>..</c> viram <c>anexo-N</c> (com a extensão do tipo
    /// declarado, quando é imagem ou PDF).
    /// </summary>
    public static string NomeDoArquivo(string? nomeArquivo, int posicao, string? tipoDeclarado)
    {
        var nome = nomeArquivo ?? string.Empty;
        var barra = nome.LastIndexOfAny(['/', '\\']);
        if (barra >= 0)
            nome = nome[(barra + 1)..];
        nome = string.Concat(nome.Where(c => !char.IsControl(c))).Trim();
        if (nome.Length == 0 || nome is "." or "..")
        {
            var tipo = tipoDeclarado?.Split(';')[0].Trim() ?? string.Empty;
            return string.Create(CultureInfo.InvariantCulture, $"anexo-{posicao}{Extensoes.GetValueOrDefault(tipo, string.Empty)}");
        }

        return Cortar(nome);
    }

    /// <summary>O texto da nota do sistema (D15): a lista dos recusados com o motivo, só para a equipe.</summary>
    public static string TextoDaNota(AnexosRecusados recusados, PoliticaAnexos politica)
    {
        ArgumentNullException.ThrowIfNull(recusados);
        ArgumentNullException.ThrowIfNull(politica);
        var texto = new StringBuilder("Anexos do e-mail que não entraram na solicitação:");
        Listar(texto, recusados, a => $"- {a.NomeArquivo}: {Motivo(a.Motivo, politica)}");
        return texto.ToString();
    }

    /// <summary>O trecho do aviso de recebida que diz ao contato quais arquivos não entraram (F15-15).</summary>
    public static string TextoDoAviso(AnexosRecusados recusados, PoliticaAnexos politica)
    {
        ArgumentNullException.ThrowIfNull(recusados);
        ArgumentNullException.ThrowIfNull(politica);
        var texto = new StringBuilder("Estes arquivos do seu e-mail não entraram na solicitação:");
        Listar(texto, recusados, a => $"- {a.NomeArquivo} ({Motivo(a.Motivo, politica)})");
        texto.Append('\n').Append("Se precisar deles, envie em outro formato ou por um link.");
        return texto.ToString();
    }

    /// <summary>O motivo em palavras, com os limites da política.</summary>
    public static string Motivo(MotivoRecusaAnexo motivo, PoliticaAnexos politica)
    {
        ArgumentNullException.ThrowIfNull(politica);
        return motivo switch
        {
            MotivoRecusaAnexo.GrandeDemais => string.Create(
                CultureInfo.InvariantCulture, $"maior que {politica.TamanhoMaximoBytes / (1024 * 1024)} MB"),
            MotivoRecusaAnexo.TipoNaoPermitido => "tipo de arquivo não permitido",
            MotivoRecusaAnexo.AlemDoLimite => string.Create(
                CultureInfo.InvariantCulture, $"além do limite de {politica.MaximoPorInteracao} anexos por e-mail"),
            _ => throw new ArgumentOutOfRangeException(nameof(motivo), motivo, "Motivo desconhecido."),
        };
    }

    private static void Listar(StringBuilder texto, AnexosRecusados recusados, Func<AnexoRecusado, string> linha)
    {
        var listados = Math.Min(RecusadosListados, recusados.Itens.Count);
        for (var i = 0; i < listados; i++)
            texto.Append('\n').Append(linha(recusados.Itens[i]));
        var restantes = recusados.Itens.Count - listados;
        if (restantes > 0)
        {
            texto.Append('\n').Append(string.Create(
                CultureInfo.InvariantCulture, $"- e mais {restantes} arquivo{(restantes == 1 ? string.Empty : "s")}"));
        }
    }

    private static MotivoRecusaAnexo? Motivo(AnexoDeEntrada anexo, string nome, PoliticaAnexos politica)
    {
        if (RecusadoPelaDeclaracao(anexo.TipoDeclarado))
            return MotivoRecusaAnexo.TipoNaoPermitido;
        if (anexo.TamanhoBytes > politica.TamanhoMaximoBytes)
            return MotivoRecusaAnexo.GrandeDemais;
        if (anexo.TamanhoBytes == 0)
            return MotivoRecusaAnexo.TipoNaoPermitido;
        return TiposDeArquivo.Detectar(anexo.Inicio, nome, anexo.TamanhoBytes) is { } tipo && politica.Permite(tipo)
            ? null
            : MotivoRecusaAnexo.TipoNaoPermitido;
    }

    private static bool RecusadoPelaDeclaracao(string? tipoDeclarado)
    {
        var tipo = tipoDeclarado?.TrimStart() ?? string.Empty;
        return PrefixosRecusados.Any(p => tipo.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Até 255 caracteres, mantendo a extensão curta e sem partir um par substituto.</summary>
    private static string Cortar(string nome)
    {
        if (nome.Length <= LimitesTexto.NomeArquivo)
            return nome;
        var extensao = Path.GetExtension(nome);
        if (extensao.Length > 16)
            extensao = string.Empty;
        var tamanho = LimitesTexto.NomeArquivo - extensao.Length;
        if (char.IsHighSurrogate(nome[tamanho - 1]))
            tamanho--;
        return string.Concat(nome.AsSpan(0, tamanho).TrimEnd(), extensao);
    }
}

/// <param name="Validos">Na ordem do MIME, até o máximo por interação; os nomes já são os que o anexo terá.</param>
/// <param name="Recusados">Nulo quando todos entraram.</param>
public sealed record SeparacaoDosAnexos(IReadOnlyList<ArquivoRecebido> Validos, AnexosRecusados? Recusados);
