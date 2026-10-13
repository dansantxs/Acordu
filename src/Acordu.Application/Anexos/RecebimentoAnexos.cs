using Acordu.Application.Abstractions;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Anexos;

/// <summary>
/// Valida e envia ao object storage os arquivos de uma interação, antes do commit (o domínio recebe só os metadados).
/// Se a gravação falhar depois, quem chamou descarta os enviados (<see cref="DescartarAsync"/>); falha no descarte fica
/// no versionamento do bucket, sem travar a resposta.
/// </summary>
public sealed class RecebimentoAnexos
{
    public static readonly TimeSpan ValidadeDownload = TimeSpan.FromMinutes(5);

    private readonly IArmazenamentoArquivos _armazenamento;

    public RecebimentoAnexos(IArmazenamentoArquivos armazenamento)
    {
        ArgumentNullException.ThrowIfNull(armazenamento);
        _armazenamento = armazenamento;
    }

    public static PoliticaAnexos Politica => TiposDeArquivo.PoliticaPadrao;

    /// <summary>Chave no bucket: <c>{tenantId}/{solicitacaoId}/{anexoId}</c>, sem o nome original (segurança §7).</summary>
    public static string Chave(Guid tenantId, Guid solicitacaoId, Guid anexoId) => $"{tenantId}/{solicitacaoId}/{anexoId}";

    public async Task<IReadOnlyList<NovoAnexo>> EnviarAsync(
        Guid tenantId, Guid solicitacaoId, IReadOnlyList<ArquivoRecebido>? arquivos, CancellationToken cancellationToken = default)
    {
        if (arquivos is null || arquivos.Count == 0)
            return [];
        if (arquivos.Count > Politica.MaximoPorInteracao)
            throw new AnexoInvalidoException($"No máximo {Politica.MaximoPorInteracao} anexos por interação.");

        var tipos = new List<string>(arquivos.Count);
        foreach (var arquivo in arquivos)
            tipos.Add(await TipoAsync(arquivo, cancellationToken));

        var enviados = new List<NovoAnexo>(arquivos.Count);
        try
        {
            for (var i = 0; i < arquivos.Count; i++)
            {
                var arquivo = arquivos[i];
                var id = Guid.CreateVersion7();
                var chave = Chave(tenantId, solicitacaoId, id);
                await using (var conteudo = arquivo.Abrir())
                    await _armazenamento.EnviarAsync(chave, conteudo, arquivo.TamanhoBytes, tipos[i], cancellationToken);
                enviados.Add(new NovoAnexo(arquivo.NomeArquivo, tipos[i], arquivo.TamanhoBytes, chave, id));
            }
        }
        catch
        {
            await DescartarAsync(enviados);
            throw;
        }

        return enviados;
    }

    /// <summary>Remove os arquivos enviados de uma gravação que falhou. Melhor esforço: erro aqui não esconde o original.</summary>
    public async Task DescartarAsync(IReadOnlyList<NovoAnexo> enviados)
    {
        ArgumentNullException.ThrowIfNull(enviados);
        foreach (var anexo in enviados)
        {
            try
            {
                await _armazenamento.RemoverAsync(anexo.ChaveArmazenamento, CancellationToken.None);
            }
#pragma warning disable CA1031 // Descarte é melhor esforço: o objeto órfão fica no bucket versionado.
            catch (Exception)
#pragma warning restore CA1031
            {
            }
        }
    }

    public Uri UrlDeDownload(AnexoArmazenado anexo)
    {
        ArgumentNullException.ThrowIfNull(anexo);
        return _armazenamento.UrlDeDownload(anexo.Chave, anexo.NomeArquivo, anexo.TipoConteudo, ValidadeDownload);
    }

    /// <summary>Tamanho (413 acima do máximo), conteúdo vazio e tipo pelo conteúdo, antes de enviar qualquer arquivo.</summary>
    private static async Task<string> TipoAsync(ArquivoRecebido arquivo, CancellationToken cancellationToken)
    {
        if (arquivo.TamanhoBytes > Politica.TamanhoMaximoBytes)
        {
            throw new ArquivoGrandeDemaisException(
                $"O arquivo {arquivo.NomeArquivo} passa de {Politica.TamanhoMaximoBytes / (1024 * 1024)} MB.");
        }

        if (arquivo.TamanhoBytes <= 0)
            throw new AnexoInvalidoException($"O arquivo {arquivo.NomeArquivo} está vazio.");

        var inicio = new byte[(int)Math.Min(TiposDeArquivo.BytesParaDetectar, arquivo.TamanhoBytes)];
        try
        {
            await using var conteudo = arquivo.Abrir();
            await conteudo.ReadExactlyAsync(inicio, cancellationToken);
        }
        catch (EndOfStreamException)
        {
            throw new AnexoInvalidoException($"O arquivo {arquivo.NomeArquivo} chegou incompleto.");
        }

        return TiposDeArquivo.Detectar(inicio, arquivo.NomeArquivo, arquivo.TamanhoBytes) is { } tipo && Politica.Permite(tipo)
            ? tipo
            : throw new AnexoInvalidoException($"O tipo do arquivo {arquivo.NomeArquivo} não é permitido.");
    }
}
