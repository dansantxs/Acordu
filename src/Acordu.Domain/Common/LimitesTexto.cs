namespace Acordu.Domain.Common;

/// <summary>
/// Tamanhos máximos dos textos do domínio. São a fonte única para a validação nas factories e,
/// depois, para o <c>HasMaxLength</c> do mapeamento EF.
/// </summary>
public static class LimitesTexto
{
    public const int Nome = 200;
    public const int NomeCategoria = 100;
    public const int NomePapel = 100;
    public const int Email = 254;
    public const int Telefone = 30;
    public const int Cargo = 100;
    public const int Observacoes = 2_000;
    public const int TextoInteracao = 10_000;
    public const int NomeArquivo = 255;
    public const int InscricaoEstadual = 20;
    public const int Logradouro = 200;
    public const int NumeroEndereco = 20;
    public const int ComplementoEndereco = 100;
    public const int Bairro = 100;
    public const int Cidade = 100;
    public const int Etiqueta = 30;

    public const int Titulo = 200;
    public const int Descricao = 10_000;
    public const int SolucaoTexto = 10_000;
    public const int SolucaoProblemaCausa = 2_000;
    public const int Motivo = 1_000;
    public const int ItemChecklist = 500;
    public const int DescricaoApontamento = 2_000;

    /// <summary>Mensagem opcional da lista de espera ("o que você quer resolver", F14o; decisão 5 do G2 do F14).</summary>
    public const int MensagemListaEspera = 1_000;
}
