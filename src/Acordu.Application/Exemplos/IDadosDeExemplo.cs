namespace Acordu.Application.Exemplos;

/// <summary>
/// Leitura e remoção dos dados de exemplo no tenant do contexto (RF-EMP-05; design do F14, D13), implementadas na
/// Infrastructure: tudo o que tem o <c>ClienteId</c> de um cliente de exemplo é exemplo.
/// </summary>
public interface IDadosDeExemplo
{
    /// <summary>
    /// Se há exemplos e se há dado próprio da empresa: a regra de "sem dado" do expurgo de cadastro
    /// (<c>ClassificacaoDoExpurgo</c>: registro de negócio, papel criado pela empresa ou vínculo de outra pessoa que não o
    /// <paramref name="criadorId"/>), sem contar o que é ligado aos clientes de exemplo.
    /// </summary>
    Task<SituacaoDosExemplos> SituacaoAsync(Guid? criadorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Apaga os clientes de exemplo e tudo o que é ligado a eles, inclusive o que a equipe criou dentro deles, na
    /// transação da unidade de trabalho (aberta aqui, se preciso; quem chama grava com <c>SalvarAsync</c>). Os clientes
    /// saem pela unidade de trabalho, com as guardas de gravação; o resto, por remoção direta no banco, no tenant do
    /// contexto. Devolve as chaves dos anexos, para remover do storage depois do commit.
    /// </summary>
    Task<ExemplosRemovidos> RemoverAsync(CancellationToken cancellationToken = default);
}

public sealed record SituacaoDosExemplos(bool Carregados, bool DadosProprios);

/// <param name="Clientes">Quantos clientes de exemplo saíram (0: não havia exemplos, nada mudou).</param>
/// <param name="Arquivos">Chaves no storage dos anexos que saíram com eles.</param>
public sealed record ExemplosRemovidos(int Clientes, IReadOnlyList<string> Arquivos);
