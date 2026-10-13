namespace Acordu.Application.Abstractions;

/// <summary>
/// Object storage dos anexos (segurança §7): bucket privado, chave sem o nome original. O download é por URL pré-assinada
/// de curta duração, sempre como <c>attachment</c>.
/// </summary>
public interface IArmazenamentoArquivos
{
    Task EnviarAsync(string chave, Stream conteudo, long tamanhoBytes, string tipoConteudo, CancellationToken cancellationToken = default);

    /// <summary>Remove o objeto (idempotente).</summary>
    Task RemoverAsync(string chave, CancellationToken cancellationToken = default);

    /// <summary>URL de download que vale por <paramref name="validade"/>, com o nome original no Content-Disposition.</summary>
    Uri UrlDeDownload(string chave, string nomeArquivo, string tipoConteudo, TimeSpan validade);
}
