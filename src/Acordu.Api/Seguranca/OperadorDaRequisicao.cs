using Acordu.Application.Nebrys;

namespace Acordu.Api.Seguranca;

/// <summary>
/// O operador da Nebrys da requisição (<see cref="IOperadorAtual"/>), definido pela barreira da área
/// (<see cref="AreaNebrys.ConferirOperadorAsync"/>) depois de conferir a conta no banco. Por escopo, definido uma vez.
/// </summary>
internal sealed class OperadorDaRequisicao : IOperadorAtual
{
    private Guid? _id;

    public Guid Id => _id ?? throw new InvalidOperationException("Sem operador da Nebrys nesta requisição.");

    public void Definir(Guid operador)
    {
        if (operador == Guid.Empty)
            throw new ArgumentException("O operador não pode ser vazio.", nameof(operador));
        if (_id is not null)
            throw new InvalidOperationException("O operador já foi definido neste escopo.");
        _id = operador;
    }
}
