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

    public Task<PaginaAtividadesDto> ListarAsync(Guid? clienteId, Guid? pessoaId, long? antes, CancellationToken cancellationToken = default)
    {
        var solicitacoes = _contexto.Permissoes.Contains(Permissoes.SolicitacaoVer);
        var tarefas = _contexto.Permissoes.Contains(Permissoes.TarefaVer);
        if (!solicitacoes && !tarefas)
            throw new AcessoNegadoException();
        return _atividades.ListarAsync(new FiltroAtividades(clienteId, pessoaId, antes, solicitacoes, tarefas), TamanhoPagina, cancellationToken);
    }
}
