using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Application.Solicitacoes;
using Acordu.Application.Tarefas;
using Acordu.Domain.Acesso;

namespace Acordu.Application.Anexos;

/// <summary>
/// Download de anexo pela equipe (contrato §7 e §8.1a): procura nas interações (com <c>solicitacao.ver</c>) e nos
/// comentários de tarefa (com <c>tarefa.ver</c>) e devolve a URL assinada de 5 minutos.
/// </summary>
public sealed class DownloadAnexos
{
    private readonly ITenantContext _contexto;
    private readonly IConsultasSolicitacao _consultas;
    private readonly IConsultasComentariosTarefa _comentarios;
    private readonly RecebimentoAnexos _anexos;

    public DownloadAnexos(
        ITenantContext contexto, IConsultasSolicitacao consultas, IConsultasComentariosTarefa comentarios, RecebimentoAnexos anexos)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(consultas);
        ArgumentNullException.ThrowIfNull(comentarios);
        ArgumentNullException.ThrowIfNull(anexos);
        _contexto = contexto;
        _consultas = consultas;
        _comentarios = comentarios;
        _anexos = anexos;
    }

    public async Task<Uri> UrlAsync(Guid anexoId, CancellationToken cancellationToken = default)
    {
        var podeSolicitacao = _contexto.Permissoes.Contains(Permissoes.SolicitacaoVer);
        var podeTarefa = _contexto.Permissoes.Contains(Permissoes.TarefaVer);
        if (!podeSolicitacao && !podeTarefa)
            throw new AcessoNegadoException();

        var anexo = (podeSolicitacao ? await _consultas.AnexoAsync(anexoId, cancellationToken) : null)
            ?? (podeTarefa ? await _comentarios.AnexoAsync(anexoId, cancellationToken) : null)
            ?? throw new NaoEncontradoException("Anexo não encontrado.");
        return _anexos.UrlDeDownload(anexo);
    }
}
