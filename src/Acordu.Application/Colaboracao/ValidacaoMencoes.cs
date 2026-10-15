using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Domain.Services;

namespace Acordu.Application.Colaboracao;

/// <summary>
/// Quem pode ser mencionado (RF-COL-02): pessoa da equipe com a permissão de ver o alvo (<c>solicitacao.ver</c> ou
/// <c>tarefa.ver</c>). Roda antes de enviar anexos, para não subir arquivo de um comentário que vai ser recusado.
/// </summary>
public sealed class ValidacaoMencoes
{
    private readonly IPessoasDaEquipe _pessoas;

    public ValidacaoMencoes(IPessoasDaEquipe pessoas)
    {
        ArgumentNullException.ThrowIfNull(pessoas);
        _pessoas = pessoas;
    }

    /// <param name="permissaoDoAlvo">Permissão de leitura de quem é mencionado.</param>
    public async Task GarantirAsync(string? texto, Guid autorId, string permissaoDoAlvo, CancellationToken cancellationToken = default)
    {
        var mencionados = Mencoes.Extrair(texto, autorId);
        if (mencionados.Count == 0)
            return;

        var podem = (await _pessoas.ComPermissaoAsync(permissaoDoAlvo, cancellationToken)).Select(p => p.UsuarioId).ToHashSet();
        if (mencionados.Any(id => !podem.Contains(id)))
            throw ErroDeCampo.Criar("texto", "Só dá para mencionar quem é da equipe e pode ver onde a menção foi feita.");
    }
}
