namespace Acordu.Domain.Entities;

/// <summary>Metadados de um arquivo de uma <see cref="Interacao"/>; o binário vive no object storage.</summary>
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
}
