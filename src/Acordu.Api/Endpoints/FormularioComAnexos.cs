using Acordu.Application.Anexos;
using Microsoft.AspNetCore.Http.Features;

namespace Acordu.Api.Endpoints;

/// <summary>
/// Corpo que aceita JSON (sem anexos) ou <c>multipart/form-data</c> (campos + <c>anexos</c>), lido à mão: o binding de
/// formulário do minimal API exigiria antiforgery, que não se aplica a uma API com Bearer.
/// </summary>
internal sealed class FormularioComAnexos
{
    /// <summary>5 arquivos de 20 MB e folga para os campos e o envelope multipart.</summary>
    public const long TamanhoMaximoCorpo = 105L * 1024 * 1024;

    public const string CampoAnexos = "anexos";

    private FormularioComAnexos(IFormCollection? formulario, IReadOnlyList<ArquivoRecebido> arquivos)
    {
        Formulario = formulario;
        Arquivos = arquivos;
    }

    /// <summary>Nulo quando o corpo veio em JSON.</summary>
    public IFormCollection? Formulario { get; }

    public IReadOnlyList<ArquivoRecebido> Arquivos { get; }

    public string? Campo(string nome) =>
        Formulario is not null && Formulario.TryGetValue(nome, out var valor) && !string.IsNullOrWhiteSpace(valor) ? valor.ToString() : null;

    public static Task<FormularioComAnexos?> LerAsync(HttpRequest requisicao, CancellationToken cancellationToken) =>
        LerAsync(requisicao, CampoAnexos, TamanhoMaximoCorpo, cancellationToken);

    /// <param name="campoArquivos">Campo dos arquivos no formulário (<c>anexos</c>, ou <c>logo</c>).</param>
    /// <param name="tamanhoMaximoCorpo">Limite do corpo inteiro; acima dele o Kestrel responde 413.</param>
    public static async Task<FormularioComAnexos?> LerAsync(
        HttpRequest requisicao, string campoArquivos, long tamanhoMaximoCorpo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requisicao);
        if (!requisicao.HasFormContentType)
            return null;
        if (requisicao.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limite)
            limite.MaxRequestBodySize = tamanhoMaximoCorpo;

        var formulario = await requisicao.ReadFormAsync(cancellationToken);
        var arquivos = formulario.Files.GetFiles(campoArquivos)
            .Select(f => new ArquivoRecebido(Path.GetFileName(f.FileName), f.Length, f.OpenReadStream))
            .ToList();
        return new FormularioComAnexos(formulario, arquivos);
    }
}
