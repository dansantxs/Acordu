using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;

namespace Acordu.Domain.ValueObjects;

/// <summary>Prazos de uma prioridade, em minutos úteis: primeira resposta e resolução.</summary>
public sealed record PrazoSla
{
    public PrazoSla(Prioridade prioridade, Duracao primeiraResposta, Duracao resolucao)
    {
        if (!Enum.IsDefined(prioridade))
            throw new ArgumentOutOfRangeException(nameof(prioridade), prioridade, "Prioridade inválida.");
        if (primeiraResposta.EhZero || resolucao.EhZero)
            throw new SlaIncompletoException($"Os prazos da prioridade {prioridade} devem ser maiores que zero.");
        if (resolucao < primeiraResposta)
            throw new SlaIncompletoException(
                $"O prazo de resolução da prioridade {prioridade} não pode ser menor que o de primeira resposta.");

        Prioridade = prioridade;
        PrimeiraResposta = primeiraResposta;
        Resolucao = resolucao;
    }

    public Prioridade Prioridade { get; }

    public Duracao PrimeiraResposta { get; }

    public Duracao Resolucao { get; }
}
