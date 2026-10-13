namespace Acordu.Application.Anexos;

/// <summary>
/// Arquivo vindo do formulário (multipart), ainda não validado. <see cref="Abrir"/> devolve um stream novo a cada chamada
/// (o do formulário é bufferizado e reposicionável).
/// </summary>
public sealed record ArquivoRecebido(string NomeArquivo, long TamanhoBytes, Func<Stream> Abrir);
