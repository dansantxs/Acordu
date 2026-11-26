namespace Acordu.Application.EntradaEmail;

/// <summary>
/// Destino → empresa (design do F15, D14; ADR 0016, item 3): a consulta global do código de entrada, sem tenant no
/// contexto, num ponto revisado da Infrastructure (<c>Persistence.EntradaEmail</c>; convenções §1). Devolve só o que a
/// entrada precisa para decidir antes de abrir o escopo da empresa.
/// </summary>
public interface IEmpresasDeEntrada
{
    /// <summary>A empresa do código, ou nula se nenhuma o tem (nunca existiu, ou foi expurgada; F15-22).</summary>
    Task<EmpresaDeEntrada?> PorCodigoAsync(string codigo, CancellationToken cancellationToken = default);
}

/// <param name="TenantId">A empresa dona do código.</param>
/// <param name="SomenteLeitura">VENCIDA ou CANCELADA (RF-EMP-02; F15-19).</param>
/// <param name="RecebimentoLigado">O recebimento por e-mail está ligado (F15-03 a F15-05).</param>
public sealed record EmpresaDeEntrada(Guid TenantId, bool SomenteLeitura, bool RecebimentoLigado);
