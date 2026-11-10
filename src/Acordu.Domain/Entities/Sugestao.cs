namespace Acordu.Domain.Entities;

/// <summary>
/// Sugestão de melhoria de alguém da equipe para a Nebrys (RF-SUG-01, spec do F13): global, fora dos tenants. A empresa e
/// a pessoa ficam só como referência (sem chave estrangeira; o expurgo de um cadastro tira as duas). Nenhuma rota
/// lista ou devolve sugestões; a operação lê em #sugestoes e no banco.
/// </summary>
public sealed class Sugestao
{
    public const int TextoMinimo = 10;
    public const int TextoMaximo = 2000;
    public const int TelaMaxima = 200;

    // Só o EF materializa.
    private Sugestao()
    {
        Texto = null!;
    }

    private Sugestao(Guid id, Guid empresaId, Guid pessoaId, string texto, string? tela, DateTimeOffset criadaEm)
    {
        Id = id;
        EmpresaId = empresaId;
        PessoaId = pessoaId;
        Texto = texto;
        Tela = tela;
        CriadaEm = criadaEm;
    }

    public Guid Id { get; private set; }

    /// <summary>Número sequencial global (o "#12" do Discord), gerado pelo banco.</summary>
    public int Numero { get; private set; }

    /// <summary>
    /// Empresa (o tenant) de onde veio, só como referência: não se chama <c>TenantId</c> porque a sugestão não é dado do
    /// tenant (não entra no filtro nem sai junto no expurgo). Nula depois que a empresa sai.
    /// </summary>
    public Guid? EmpresaId { get; private set; }

    /// <summary>Quem sugeriu, como referência; nula depois que a empresa sai.</summary>
    public Guid? PessoaId { get; private set; }

    public string Texto { get; private set; }

    /// <summary>Rota modelo da tela de onde veio (<c>/tarefas</c>), se o app mandou.</summary>
    public string? Tela { get; private set; }

    public DateTimeOffset CriadaEm { get; private set; }

    /// <summary>Quando saiu em #sugestoes; nulo enquanto pendente (o tick reenvia).</summary>
    public DateTimeOffset? AvisadaEm { get; private set; }

    public static Sugestao Criar(Guid id, Guid empresaId, Guid pessoaId, string texto, string? tela, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(texto);
        var limpo = texto.Trim();
        if (limpo.Length < TextoMinimo)
            throw new ArgumentException($"Escreva pelo menos {TextoMinimo} caracteres.", nameof(texto));
        if (limpo.Length > TextoMaximo)
            throw new ArgumentException($"A sugestão pode ter até {TextoMaximo} caracteres.", nameof(texto));
        var telaLimpa = string.IsNullOrWhiteSpace(tela) ? null : tela.Trim();
        if (telaLimpa?.Length > TelaMaxima)
            telaLimpa = telaLimpa[..TelaMaxima];
        return new Sugestao(id, empresaId, pessoaId, limpo, telaLimpa, agora);
    }
}
