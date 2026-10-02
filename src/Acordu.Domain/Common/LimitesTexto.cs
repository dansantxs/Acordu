namespace Acordu.Domain.Common;

/// <summary>
/// Tamanhos máximos dos textos do domínio. São a fonte única para a validação nas factories e,
/// depois, para o <c>HasMaxLength</c> do mapeamento EF.
/// </summary>
public static class LimitesTexto
{
    public const int Titulo = 200;
    public const int Descricao = 10_000;
    public const int SolucaoTexto = 10_000;
    public const int SolucaoProblemaCausa = 2_000;
    public const int Motivo = 1_000;
}
