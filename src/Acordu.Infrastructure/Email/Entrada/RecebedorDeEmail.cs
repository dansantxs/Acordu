using Acordu.Application.Common;
using Acordu.Application.EntradaEmail;
using Acordu.Infrastructure.MultiTenancy;
using Acordu.Infrastructure.Persistence.EntradaEmail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Acordu.Infrastructure.Email.Entrada;

/// <summary>
/// O e-mail que chegou à rota de entrada (RF-SOL-11; design do F15, D1, D6, D12 e D14; ADR 0016, itens 2, 3 e 8), na
/// ordem do D12 depois do token e do tamanho declarado (que a Api confere): destino → empresa (desconhecida, só leitura,
/// desligada) → leitura do MIME (tamanho real) → escopo da empresa, onde o automático, o laço e o filtro do provedor viram
/// descarte registrado (F15-85, F15-87, F15-88). Os descartes antes da empresa não leem o corpo nem gravam nada (F15-05,
/// F15-19, F15-22). Cada e-mail deixa uma linha de log só com o desfecho (convenções §5): nada do
/// endereço, do assunto nem do conteúdo.
/// </summary>
public sealed partial class RecebedorDeEmail
{
    private readonly IServiceScopeFactory _escopos;
    private readonly OpcoesEmailEntrada _opcoes;
    private readonly ILogger<RecebedorDeEmail> _logger;

    public RecebedorDeEmail(IServiceScopeFactory escopos, OpcoesEmailEntrada opcoes, ILogger<RecebedorDeEmail> logger)
    {
        ArgumentNullException.ThrowIfNull(escopos);
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(logger);
        _escopos = escopos;
        _opcoes = opcoes;
        _logger = logger;
    }

    /// <param name="envelope">O que o Worker manda à parte do MIME: destinatário, remetente e veredito do filtro.</param>
    /// <param name="corpo">O MIME bruto, em stream.</param>
    public async Task<DesfechoEntrada> ReceberAsync(EnvelopeDeEntrada envelope, Stream corpo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(corpo);
        var desfecho = await AvaliarAsync(envelope, corpo, cancellationToken);
        Recebido(_logger, desfecho);
        return desfecho;
    }

    private async Task<DesfechoEntrada> AvaliarAsync(EnvelopeDeEntrada envelope, Stream corpo, CancellationToken cancellationToken)
    {
        if (DestinoDeEntrada.Ler(envelope.Destinatario, _opcoes) is not { } destino)
            return DesfechoEntrada.Desconhecida;

        EmpresaDeEntrada? empresa;
        await using (var consulta = _escopos.CreateAsyncScope())
            empresa = await consulta.ServiceProvider.GetRequiredService<IEmpresasDeEntrada>().PorCodigoAsync(destino.Codigo, cancellationToken);
        if (AdmissaoDeEntrada.Avaliar(empresa) is { } descarte)
            return descarte;

        MensagemDeEntrada? mensagem;
        try
        {
            mensagem = await LeitorMime.LerAsync(corpo, _opcoes.TamanhoMaximoBytes, cancellationToken);
        }
        catch (CorpoGrandeDemaisException)
        {
            return DesfechoEntrada.GrandeDemais;
        }

        if (mensagem is null)
            return DesfechoEntrada.Malformada;
        mensagem = mensagem with { RemetenteDoEnvelope = envelope.Remetente, Filtro = envelope.Filtro };

        // Escopo próprio com o tenant do destino fixado, como os jobs (ADR 0016, item 3): filtros e guardas valem.
        await using var escopo = _escopos.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<ContextoTenant>().DefinirEquipe(empresa!.TenantId);
        try
        {
            return await escopo.ServiceProvider.GetRequiredService<ComandosEntradaEmail>()
                .ReceberAsync(mensagem, destino, cancellationToken);
        }
        catch (DbUpdateException ex) when (EmailsRecebidos.ChaveRepetida(ex))
        {
            return DesfechoEntrada.Repetida;
        }
        catch (EmpresaSomenteLeituraException)
        {
            // A empresa ficou só leitura entre a consulta e a gravação: a guarda recusou, e nada foi gravado (F15-19).
            return DesfechoEntrada.SomenteLeitura;
        }
    }

    [LoggerMessage(EventId = 410, Level = LogLevel.Information, Message = "Entrada por e-mail: {Desfecho}.")]
    private static partial void Recebido(ILogger logger, DesfechoEntrada desfecho);
}

/// <summary>
/// Os cabeçalhos do Worker de entrada além do MIME (design do F15, D1 e D11).
/// </summary>
/// <param name="Destinatario">O destinatário do envelope (<c>X-Email-Destinatario</c>).</param>
/// <param name="Remetente">O remetente do envelope (<c>X-Email-Remetente</c>); vazio é devolução; nulo sem o cabeçalho.</param>
/// <param name="Filtro">O veredito do filtro do provedor (<c>X-Email-Filtro</c>); nulo sem o cabeçalho.</param>
public sealed record EnvelopeDeEntrada(string? Destinatario, string? Remetente = null, string? Filtro = null);
