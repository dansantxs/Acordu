namespace Acordu.Application.Common;

/// <summary>Página de uma listagem (contrato §1): <c>{ itens, pagina, tamanhoPagina, total }</c>.</summary>
public sealed record ResultadoPaginado<T>(IReadOnlyList<T> Itens, int Pagina, int TamanhoPagina, int Total);

/// <summary>Parâmetros de paginação normalizados: página ≥ 1, tamanho entre 1 e 200 (padrão 50).</summary>
public readonly record struct Paginacao
{
    public const int TamanhoPadrao = 50;
    public const int TamanhoMaximo = 200;

    public Paginacao(int? pagina, int? tamanhoPagina)
    {
        Pagina = pagina is > 0 ? pagina.Value : 1;
        TamanhoPagina = tamanhoPagina is > 0 ? Math.Min(tamanhoPagina.Value, TamanhoMaximo) : TamanhoPadrao;
    }

    public int Pagina { get; }

    public int TamanhoPagina { get; }

    public int Pular => (Pagina - 1) * TamanhoPagina;
}
