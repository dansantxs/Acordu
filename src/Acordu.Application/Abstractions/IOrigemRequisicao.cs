namespace Acordu.Application.Abstractions;

/// <summary>De onde veio a requisição corrente (IP do cliente, já resolvido atrás do proxy). Nulo fora de requisição (jobs).</summary>
public interface IOrigemRequisicao
{
    string? Ip { get; }
}
