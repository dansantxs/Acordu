namespace Acordu.Application.Anexos;

/// <summary>Anexo de uma interação (o download vai por <c>/anexos/{id}/download</c>).</summary>
public sealed record AnexoDto(Guid Id, string NomeArquivo, string TipoConteudo, long TamanhoBytes);

/// <summary>
/// Resposta dos <c>/download</c>: a URL assinada de curta duração, para o navegador abrir. JSON em vez de 302 porque o SPA
/// manda o token no cabeçalho (ADR 0007), e um redirect seguido pelo <c>fetch</c> não vira download.
/// </summary>
public sealed record LinkDownloadDto(Uri Url);

/// <summary>Onde está o arquivo e como entregá-lo.</summary>
public sealed record AnexoArmazenado(string Chave, string NomeArquivo, string TipoConteudo);
