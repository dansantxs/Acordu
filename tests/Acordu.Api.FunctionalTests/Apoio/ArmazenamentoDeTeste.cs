using System.Collections.Concurrent;
using Acordu.Application.Abstractions;

namespace Acordu.Api.FunctionalTests.Apoio;

/// <summary>Object storage em memória; a URL "assinada" leva a chave, o nome e o tipo para os testes conferirem.</summary>
internal sealed class ArmazenamentoDeTeste : IArmazenamentoArquivos
{
    public const string Base = "https://armazenamento.teste/";

    private readonly ConcurrentDictionary<string, (byte[] Conteudo, string Tipo)> _objetos = new();

    public bool Existe(string chave) => _objetos.ContainsKey(chave);

    public byte[] Conteudo(string chave) => _objetos[chave].Conteudo;

    public int ComPrefixo(string prefixo) => _objetos.Keys.Count(k => k.StartsWith(prefixo, StringComparison.Ordinal));

    public async Task EnviarAsync(
        string chave, Stream conteudo, long tamanhoBytes, string tipoConteudo, CancellationToken cancellationToken = default)
    {
        using var copia = new MemoryStream();
        await conteudo.CopyToAsync(copia, cancellationToken);
        _objetos[chave] = (copia.ToArray(), tipoConteudo);
    }

    public Task RemoverAsync(string chave, CancellationToken cancellationToken = default)
    {
        _objetos.TryRemove(chave, out _);
        return Task.CompletedTask;
    }

    public Uri UrlDeDownload(string chave, string nomeArquivo, string tipoConteudo, TimeSpan validade) =>
        new($"{Base}{chave}?nome={Uri.EscapeDataString(nomeArquivo)}&tipo={Uri.EscapeDataString(tipoConteudo)}&validade={validade.TotalMinutes}");
}
