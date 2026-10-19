using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Domain.Acesso;

namespace Acordu.Application.Atividades;

/// <summary>
/// Feed de atividade (contrato §11f): do mais recente, 50 por página com cursor, filtrável por cliente e por pessoa.
/// Cada linha só aparece para quem pode ver o alvo (solicitação ou tarefa).
/// </summary>
public sealed class LeituraAtividades
{
    public const int TamanhoPagina = 50;

    private readonly ITenantContext _contexto;
    private readonly IAtividades _atividades;

    public LeituraAtividades(ITenantContext contexto, IAtividades atividades)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(atividades);
        _contexto = contexto;
        _atividades = atividades;
    }

    /// <summary><paramref name="solicitacaoId"/> restringe ao histórico de uma solicitação (o painel de detalhe, F2e).</summary>
    public Task<PaginaAtividadesDto> ListarAsync(
        Guid? clienteId, Guid? pessoaId, long? antes, Guid? solicitacaoId = null, CancellationToken cancellationToken = default)
    {
        var solicitacoes = _contexto.Permissoes.Contains(Permissoes.SolicitacaoVer);
        var tarefas = _contexto.Permissoes.Contains(Permissoes.TarefaVer);
        if (!solicitacoes && !tarefas)
            throw new AcessoNegadoException();
        return _atividades.ListarAsync(new FiltroAtividades(clienteId, pessoaId, antes, solicitacoes, tarefas, solicitacaoId), TamanhoPagina, cancellationToken);
    }
}
