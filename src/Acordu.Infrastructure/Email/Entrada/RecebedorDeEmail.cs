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
/// desligada) → leitura do MIME (tamanho real) → escopo da empresa. Os descartes antes da empresa não leem o corpo nem
/// gravam nada (F15-05, F15-19, F15-22). Cada e-mail deixa uma linha de log só com o desfecho (convenções §5): nada do
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

    /// <param name="destinatario">O destinatário do envelope (<c>X-Email-Destinatario</c> do Worker).</param>
    /// <param name="corpo">O MIME bruto, em stream.</param>
    public async Task<DesfechoEntrada> ReceberAsync(string? destinatario, Stream corpo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(corpo);
        var desfecho = await AvaliarAsync(destinatario, corpo, cancellationToken);
        Recebido(_logger, desfecho);
        return desfecho;
    }

    private async Task<DesfechoEntrada> AvaliarAsync(string? destinatario, Stream corpo, CancellationToken cancellationToken)
    {
        if (DestinoDeEntrada.Ler(destinatario, _opcoes) is not { } destino)
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
