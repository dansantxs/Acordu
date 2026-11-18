using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Empresas;

/// <summary>
/// Prazos da situação da empresa (design do F14, §1.2), da configuração: <c>Empresas__DiasDeTeste</c> (padrão 30, decisão
/// 1 do G2), <c>Empresas__DiasAteExpurgo</c> (padrão 30, decisão 2 do G2), <c>Empresas__DiasFaixaTeste</c> (padrão 7,
/// decisão 3 do G2: a janela antes do fim do teste em que a SPA mostra a faixa, devolvida no <c>/auth/eu</c>),
/// <c>Empresas__AvisosAntesDoFim</c> (padrão <c>7,1</c>) e <c>Empresas__AvisoAntesDoExpurgo</c> (padrão 7): os e-mails de
/// situação aos Donos (decisão 3 do G2, F14j).
/// </summary>
public sealed record OpcoesEmpresas
{
    public const int DiasDeTestePadrao = 30;
    public const int DiasAteExpurgoPadrao = 30;
    public const int DiasFaixaTestePadrao = 7;
    public const int AvisoAntesDoExpurgoPadrao = 7;

    /// <summary>Os e-mails 7 e 1 dia antes do fim do teste (decisão 3 do G2).</summary>
    public static readonly IReadOnlyList<int> AvisosAntesDoFimPadrao = [7, 1];

    public OpcoesEmpresas(
        int diasDeTeste = DiasDeTestePadrao,
        int diasAteExpurgo = DiasAteExpurgoPadrao,
        int diasFaixaTeste = DiasFaixaTestePadrao,
        IEnumerable<int>? avisosAntesDoFim = null,
        int avisoAntesDoExpurgo = AvisoAntesDoExpurgoPadrao)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(diasDeTeste);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(diasAteExpurgo);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(diasFaixaTeste);
        DiasDeTeste = diasDeTeste;
        DiasAteExpurgo = diasAteExpurgo;
        DiasFaixaTeste = diasFaixaTeste;
        PrazosDosAvisos = new PrazosAvisosSituacao(
            avisosAntesDoFim ?? AvisosAntesDoFimPadrao, avisoAntesDoExpurgo, TimeSpan.FromDays(diasAteExpurgo));
    }

    public int DiasDeTeste { get; }

    public int DiasAteExpurgo { get; }

    /// <summary>Dias antes do fim do teste em que a faixa do teste aparece na SPA (F14-10, F14-11).</summary>
    public int DiasFaixaTeste { get; }

    /// <summary>Quando saem os e-mails de situação (passo <c>avisos-situacao</c> do diário, F14-39 a F14-42).</summary>
    public PrazosAvisosSituacao PrazosDosAvisos { get; }

    /// <summary>Duração do teste: o fim do teste é o instante do cadastro mais ela (F14-01).</summary>
    public TimeSpan DuracaoDoTeste => TimeSpan.FromDays(DiasDeTeste);

    /// <summary>Do início do só leitura até o expurgo (RF-EMP-03).</summary>
    public TimeSpan PrazoAteExpurgo => TimeSpan.FromDays(DiasAteExpurgo);
}
