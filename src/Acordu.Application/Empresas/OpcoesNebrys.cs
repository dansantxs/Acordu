namespace Acordu.Application.Empresas;

/// <summary>
/// Canal para falar com a Nebrys (decisão 14 do G2 do F14, design D15): <c>Nebrys__Contato</c>, que em produção vem da
/// variável <c>CONTATO_NEBRYS</c> do GitHub (<c>contato@acordu.app.br</c>), a mesma do <c>VITE_CONTATO_NEBRYS</c> do SPA.
/// Sem ela (desenvolvimento, testes), os textos dizem só "fale com a Nebrys".
/// </summary>
public sealed record OpcoesNebrys
{
    public OpcoesNebrys(string? contato = null)
    {
        Contato = string.IsNullOrWhiteSpace(contato) ? null : contato.Trim();
    }

    public string? Contato { get; }

    /// <summary>"fale com a Nebrys: {canal}", ou só "fale com a Nebrys" sem o canal na configuração.</summary>
    public string FaleComANebrys => Contato is null ? "fale com a Nebrys" : $"fale com a Nebrys: {Contato}";
}
