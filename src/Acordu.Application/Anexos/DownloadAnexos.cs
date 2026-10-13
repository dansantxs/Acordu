using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Application.Solicitacoes;
using Acordu.Domain.Acesso;

namespace Acordu.Application.Anexos;

/// <summary>Download de anexo pela equipe (contrato §7): checa a permissão e devolve a URL assinada de 5 minutos.</summary>
public sealed class DownloadAnexos
{
    private readonly ITenantContext _contexto;
    private readonly IConsultasSolicitacao _consultas;
    private readonly RecebimentoAnexos _anexos;

    public DownloadAnexos(ITenantContext contexto, IConsultasSolicitacao consultas, RecebimentoAnexos anexos)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(consultas);
        ArgumentNullException.ThrowIfNull(anexos);
        _contexto = contexto;
        _consultas = consultas;
        _anexos = anexos;
    }

    public async Task<Uri> UrlAsync(Guid anexoId, CancellationToken cancellationToken = default)
    {
        _contexto.Exigir(Permissoes.SolicitacaoVer);
        var anexo = await _consultas.AnexoAsync(anexoId, cancellationToken) ?? throw new NaoEncontradoException("Anexo não encontrado.");
        return _anexos.UrlDeDownload(anexo);
    }
}
