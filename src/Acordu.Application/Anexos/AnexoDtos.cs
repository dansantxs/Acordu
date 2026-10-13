namespace Acordu.Application.Anexos;

/// <summary>Anexo de uma interação (o download vai por <c>/anexos/{id}/download</c>).</summary>
public sealed record AnexoDto(Guid Id, string NomeArquivo, string TipoConteudo, long TamanhoBytes);

/// <summary>Onde está o arquivo e como entregá-lo.</summary>
public sealed record AnexoArmazenado(string Chave, string NomeArquivo, string TipoConteudo);
