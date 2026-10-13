using Acordu.Application.Abstractions;
using Acordu.Application.Anexos;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;

namespace Acordu.Application.Configuracoes;

/// <summary>
/// Logotipo do portal (RF-POR-09): PNG, JPEG ou WebP até 512 KB, no mesmo bucket dos anexos. Cada envio usa uma chave
/// nova (o navegador não fica com o antigo em cache) e o anterior é removido depois do commit. Exibido por URL assinada
/// de 1 hora: nada público no bucket.
/// </summary>
public sealed class LogotipoPortal
{
    public const long TamanhoMaximoBytes = 512 * 1024;
    public static readonly TimeSpan ValidadeUrl = TimeSpan.FromHours(1);

    private static readonly Dictionary<string, string> Extensoes = new(StringComparer.Ordinal)
    {
        [TiposDeArquivo.Png] = ".png",
        [TiposDeArquivo.Jpeg] = ".jpg",
        [TiposDeArquivo.Webp] = ".webp",
    };

    private readonly ITenantContext _contexto;
    private readonly ITenantAtual _tenant;
    private readonly IArmazenamentoArquivos _armazenamento;
    private readonly IUnitOfWork _unidade;

    public LogotipoPortal(ITenantContext contexto, ITenantAtual tenant, IArmazenamentoArquivos armazenamento, IUnitOfWork unidade)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(armazenamento);
        ArgumentNullException.ThrowIfNull(unidade);
        _contexto = contexto;
        _tenant = tenant;
        _armazenamento = armazenamento;
        _unidade = unidade;
    }

    /// <summary>URL assinada do logotipo do tenant do contexto, ou nula sem logotipo.</summary>
    public async Task<Uri?> UrlAsync(CancellationToken cancellationToken = default) =>
        Url((await _tenant.ObterAsync(cancellationToken)).LogoChave);

    public Uri? Url(string? chave)
    {
        if (chave is null)
            return null;
        var extensao = Path.GetExtension(chave);
        var tipo = Extensoes.FirstOrDefault(e => e.Value == extensao).Key ?? TiposDeArquivo.Png;
        return _armazenamento.UrlDeDownload(chave, "logotipo" + extensao, tipo, ValidadeUrl);
    }

    public async Task EnviarAsync(ArquivoRecebido? arquivo, CancellationToken cancellationToken = default)
    {
        _contexto.Exigir(Permissoes.ConfiguracaoGerenciar);
        if (arquivo is null)
            throw ErroDeCampo.Criar("logo", "Envie a imagem no campo logo.");
        if (arquivo.TamanhoBytes > TamanhoMaximoBytes)
            throw new ArquivoGrandeDemaisException("O logotipo pode ter até 512 KB.");
        if (arquivo.TamanhoBytes <= 0)
            throw ErroDeCampo.Criar("logo", "A imagem está vazia.");

        var inicio = new byte[(int)Math.Min(TiposDeArquivo.BytesParaDetectar, arquivo.TamanhoBytes)];
        await using (var leitura = arquivo.Abrir())
            await leitura.ReadAtLeastAsync(inicio, inicio.Length, throwOnEndOfStream: false, cancellationToken);
        var tipo = TiposDeArquivo.Detectar(inicio, arquivo.NomeArquivo, arquivo.TamanhoBytes);
        if (tipo is null || !Extensoes.TryGetValue(tipo, out var extensao))
            throw ErroDeCampo.Criar("logo", "O logotipo precisa ser PNG, JPEG ou WebP.");

        var tenant = await _tenant.ObterAsync(cancellationToken);
        var anterior = tenant.LogoChave;
        var chave = $"{tenant.Id}/portal/logo-{Guid.CreateVersion7():N}{extensao}";
        await using (var conteudo = arquivo.Abrir())
            await _armazenamento.EnviarAsync(chave, conteudo, arquivo.TamanhoBytes, tipo, cancellationToken);
        try
        {
            tenant.DefinirLogo(chave);
            await _unidade.SalvarAsync(cancellationToken);
        }
        catch
        {
            await RemoverEmSilencioAsync(chave);
            throw;
        }

        await RemoverEmSilencioAsync(anterior);
    }

    public async Task RemoverAsync(CancellationToken cancellationToken = default)
    {
        _contexto.Exigir(Permissoes.ConfiguracaoGerenciar);
        var tenant = await _tenant.ObterAsync(cancellationToken);
        var anterior = tenant.LogoChave;
        if (anterior is null)
            return;
        tenant.DefinirLogo(null);
        await _unidade.SalvarAsync(cancellationToken);
        await RemoverEmSilencioAsync(anterior);
    }

    /// <summary>Melhor esforço: o arquivo que sobrar fica no bucket versionado.</summary>
    private async Task RemoverEmSilencioAsync(string? chave)
    {
        if (chave is null)
            return;
        try
        {
            await _armazenamento.RemoverAsync(chave, CancellationToken.None);
        }
#pragma warning disable CA1031 // Limpeza de arquivo antigo não pode derrubar a troca já gravada.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }
}
