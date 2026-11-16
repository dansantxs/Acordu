namespace Acordu.Application.Empresas;

/// <summary>
/// Prazos da situação da empresa (design do F14, §1.2), da configuração: <c>Empresas__DiasDeTeste</c> (padrão 30, decisão
/// 1 do G2), <c>Empresas__DiasAteExpurgo</c> (padrão 30, decisão 2 do G2) e <c>Empresas__DiasFaixaTeste</c> (padrão 7,
/// decisão 3 do G2: a janela antes do fim do teste em que a SPA mostra a faixa, devolvida no <c>/auth/eu</c>). Os demais
/// valores do F14 entram com os PRs que os usam.
/// </summary>
public sealed record OpcoesEmpresas
{
    public const int DiasDeTestePadrao = 30;
    public const int DiasAteExpurgoPadrao = 30;
    public const int DiasFaixaTestePadrao = 7;

    public OpcoesEmpresas(
        int diasDeTeste = DiasDeTestePadrao,
        int diasAteExpurgo = DiasAteExpurgoPadrao,
        int diasFaixaTeste = DiasFaixaTestePadrao)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(diasDeTeste);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(diasAteExpurgo);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(diasFaixaTeste);
        DiasDeTeste = diasDeTeste;
        DiasAteExpurgo = diasAteExpurgo;
        DiasFaixaTeste = diasFaixaTeste;
    }

    public int DiasDeTeste { get; }

    public int DiasAteExpurgo { get; }

    /// <summary>Dias antes do fim do teste em que a faixa do teste aparece na SPA (F14-10, F14-11).</summary>
    public int DiasFaixaTeste { get; }

    /// <summary>Duração do teste: o fim do teste é o instante do cadastro mais ela (F14-01).</summary>
    public TimeSpan DuracaoDoTeste => TimeSpan.FromDays(DiasDeTeste);

    /// <summary>Do início do só leitura até o expurgo (RF-EMP-03).</summary>
    public TimeSpan PrazoAteExpurgo => TimeSpan.FromDays(DiasAteExpurgo);
}
