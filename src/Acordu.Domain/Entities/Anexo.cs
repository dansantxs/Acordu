using Acordu.Domain.Common;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.Entities;

/// <summary>
/// Metadados de um arquivo de uma <see cref="Interacao"/> ou de um <see cref="ComentarioTarefa"/>; o binário vive no object
/// storage.
/// </summary>
public sealed class Anexo
{
    internal Anexo(Guid id, string nomeArquivo, string tipoConteudo, long tamanhoBytes, string chaveArmazenamento)
    {
        Id = id;
        NomeArquivo = nomeArquivo;
        TipoConteudo = tipoConteudo;
        TamanhoBytes = tamanhoBytes;
        ChaveArmazenamento = chaveArmazenamento;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private Anexo()
    {
        NomeArquivo = null!;
        TipoConteudo = null!;
        ChaveArmazenamento = null!;
    }

    public Guid Id { get; private set; }

    public string NomeArquivo { get; private set; }

    public string TipoConteudo { get; private set; }

    public long TamanhoBytes { get; private set; }

    public string ChaveArmazenamento { get; private set; }

    /// <summary>Valida os metadados contra a política (<see cref="AnexoInvalidoException"/>).</summary>
    internal static Anexo Criar(NovoAnexo anexo, PoliticaAnexos politica)
    {
        ArgumentNullException.ThrowIfNull(anexo);
        var nome = Texto.Obrigatorio(anexo.NomeArquivo, LimitesTexto.NomeArquivo, nameof(anexo));
        if (nome.IndexOfAny(['/', '\\']) >= 0 || nome is "." or "..")
            throw new AnexoInvalidoException($"Nome de arquivo inválido: {nome}.");
        if (string.IsNullOrWhiteSpace(anexo.ChaveArmazenamento))
            throw new ArgumentException("A chave de armazenamento é obrigatória.", nameof(anexo));
        if (anexo.TamanhoBytes <= 0 || anexo.TamanhoBytes > politica.TamanhoMaximoBytes)
            throw new AnexoInvalidoException($"O arquivo {nome} excede o tamanho permitido ou está vazio.");
        if (string.IsNullOrWhiteSpace(anexo.TipoConteudo) || !politica.Permite(anexo.TipoConteudo))
            throw new AnexoInvalidoException($"O tipo do arquivo {nome} não é permitido.");

        return new Anexo(
            anexo.Id ?? Guid.CreateVersion7(), nome, anexo.TipoConteudo.Trim().ToLowerInvariant(), anexo.TamanhoBytes,
            anexo.ChaveArmazenamento);
    }
}
