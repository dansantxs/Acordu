namespace Acordu.Application.Empresas;

/// <summary>
/// Prazos da situação da empresa (design do F14, §1.2), da configuração: <c>Empresas__DiasDeTeste</c> (padrão 30, decisão
/// 1 do G2) e <c>Empresas__DiasAteExpurgo</c> (padrão 30, decisão 2 do G2). Os demais valores do F14 entram com os PRs
/// que os usam.
/// </summary>
public sealed record OpcoesEmpresas
{
    public const int DiasDeTestePadrao = 30;
    public const int DiasAteExpurgoPadrao = 30;

    public OpcoesEmpresas(int diasDeTeste = DiasDeTestePadrao, int diasAteExpurgo = DiasAteExpurgoPadrao)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(diasDeTeste);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(diasAteExpurgo);
        DiasDeTeste = diasDeTeste;
        DiasAteExpurgo = diasAteExpurgo;
    }

    public int DiasDeTeste { get; }

    public int DiasAteExpurgo { get; }

    /// <summary>Duração do teste: o fim do teste é o instante do cadastro mais ela (F14-01).</summary>
    public TimeSpan DuracaoDoTeste => TimeSpan.FromDays(DiasDeTeste);

    /// <summary>Do início do só leitura até o expurgo (RF-EMP-03).</summary>
    public TimeSpan PrazoAteExpurgo => TimeSpan.FromDays(DiasAteExpurgo);
}
