using System.Globalization;
using Acordu.Application.Abstractions;
using Acordu.Application.Abstractions.Operacao;
using Acordu.Application.Common;
using Acordu.Domain.Entities;

namespace Acordu.Application.Sugestoes;

/// <summary>Resposta do envio: o número que a pessoa pode citar depois.</summary>
public sealed record SugestaoCriadaDto(Guid Id, int Numero);

/// <summary>
/// "Sugerir melhoria" (RF-SUG-01; spec do F13, F13e): qualquer pessoa da equipe, sem permissão nova (decisão 5), até 5
/// por hora. Grava fora dos tenants e avisa em #sugestoes na hora, com prazo; se não sair, fica pendente e o tick
/// reenvia (F13-29). O aviso leva o número, o texto, a tela e o nome da empresa, nunca a pessoa (decisão 4).
/// </summary>
public sealed class ComandosSugestao
{
    public const int LimitePorHora = 5;

    /// <summary>Prazo do aviso na requisição: passou, a resposta sai e o tick reenvia.</summary>
    public static readonly TimeSpan PrazoDoAviso = TimeSpan.FromSeconds(3);

    private readonly ITenantContext _contexto;
    private readonly ISugestoes _sugestoes;
    private readonly IMensageiroOperacao _mensageiro;
    private readonly TimeProvider _relogio;

    public ComandosSugestao(ITenantContext contexto, ISugestoes sugestoes, IMensageiroOperacao mensageiro, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(sugestoes);
        ArgumentNullException.ThrowIfNull(mensageiro);
        ArgumentNullException.ThrowIfNull(relogio);
        _contexto = contexto;
        _sugestoes = sugestoes;
        _mensageiro = mensageiro;
        _relogio = relogio;
    }

    public async Task<SugestaoCriadaDto> EnviarAsync(string texto, string? tela, CancellationToken cancellationToken = default)
    {
        if (_contexto.EhCliente || _contexto.TenantId is not { } tenant)
            throw new AcessoNegadoException("Só a equipe sugere melhorias.");
        var usuario = _contexto.Usuario();
        var agora = _relogio.GetUtcNow();
        if (await _sugestoes.DaPessoaDesdeAsync(usuario, agora.AddHours(-1), cancellationToken) >= LimitePorHora)
            throw new LimiteExcedidoException("Muitas sugestões em pouco tempo. Tente de novo mais tarde.");

        var sugestao = Sugestao.Criar(Guid.NewGuid(), tenant, usuario, texto, tela, agora);
        var numero = await _sugestoes.GravarAsync(sugestao, cancellationToken);

        using var prazo = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        prazo.CancelAfter(PrazoDoAviso);
        try
        {
            if (await _sugestoes.ParaAvisoAsync(sugestao.Id, prazo.Token) is { } paraAviso)
                await AvisarAsync(paraAviso, prazo.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Fica pendente; o tick reenvia.
        }

        return new SugestaoCriadaDto(sugestao.Id, numero);
    }

    /// <summary>Tick: reenvia as pendentes há mais de um minuto (as recentes ainda podem estar saindo na requisição).</summary>
    public async Task<int> ReenviarPendentesAsync(CancellationToken cancellationToken = default)
    {
        if (!_mensageiro.Configurado(CanalOperacao.Sugestoes))
            return 0;
        var enviadas = 0;
        foreach (var pendente in await _sugestoes.PendentesAsync(_relogio.GetUtcNow().AddMinutes(-1), 20, cancellationToken))
        {
            if (!await AvisarAsync(pendente, cancellationToken))
                break;
            enviadas++;
        }

        return enviadas;
    }

    internal static string Texto(SugestaoParaAviso sugestao) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"**Sugestão #{sugestao.Numero}** · {sugestao.Empresa}{(sugestao.Tela is { } tela ? $" · tela `{tela.Replace('`', '\'')}`" : "")}\n{sugestao.Texto}");

    private async Task<bool> AvisarAsync(SugestaoParaAviso sugestao, CancellationToken cancellationToken)
    {
        if (!await _mensageiro.EnviarAsync(CanalOperacao.Sugestoes, Texto(sugestao), cancellationToken))
            return false;
        await _sugestoes.MarcarAvisadaAsync(sugestao.Id, _relogio.GetUtcNow(), CancellationToken.None);
        return true;
    }
}
