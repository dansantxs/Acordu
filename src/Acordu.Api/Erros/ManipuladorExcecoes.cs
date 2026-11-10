using Acordu.Api.Operacao;
using Acordu.Api.Seguranca;
using Acordu.Application.Abstractions.Operacao;
using Acordu.Application.Anexos;
using Acordu.Application.Common;
using Acordu.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Acordu.Api.Erros;

/// <summary>
/// Converte exceções em Problem Details (RFC 9457, contrato da API §13). Regra de negócio → 422; entrada malformada →
/// 400 <c>validacao</c> com <c>errors</c> por campo; versão divergente → 409. Qualquer outra (inclusive violação de
/// isolamento, que é bug) → 500 sem detalhes.
/// </summary>
internal static partial class ManipuladorExcecoes
{
    public const string BaseTipos = "https://acordu.app.br/erros/";

    public static async Task TratarAsync(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);
        var excecao = http.Features.Get<IExceptionHandlerFeature>()?.Error;
        var problema = Classificar(excecao);
        if (problema.Status == StatusCodes.Status500InternalServerError)
        {
            var logger = http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ManipuladorExcecoes));
            var contexto = ContextoDoErro(http);
            using (logger.BeginScope(contexto))
                RegistrarErroInesperado(logger, excecao);
            if (excecao is not null)
                await AvisarAsync(http, excecao, contexto, logger);
        }

        http.Response.StatusCode = problema.Status ?? StatusCodes.Status500InternalServerError;
        var servico = http.RequestServices.GetRequiredService<IProblemDetailsService>();
        await servico.WriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problema, Exception = excecao });
    }

    /// <summary>
    /// Contexto do erro no log (RNF-OPS-01): a rota modelo (<c>/api/v1/tarefas/{id}</c>, nunca o caminho com os valores
    /// nem a query), o método e os identificadores de tenant e usuário. Nada de corpo, cabeçalho, e-mail ou nome.
    /// </summary>
    internal static Dictionary<string, object?> ContextoDoErro(HttpContext http)
    {
        var rota = http.Features.Get<IExceptionHandlerFeature>()?.Endpoint is RouteEndpoint endpoint
            ? endpoint.RoutePattern.RawText
            : null;
        var contexto = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["rota"] = rota ?? "(sem rota)",
            ["metodo"] = http.Request.Method,
        };
        if (http.User.FindFirst(ClaimsAcordu.Tenant)?.Value is { Length: > 0 } tenant)
            contexto["tenantId"] = tenant;
        if (http.User.FindFirst(ClaimsAcordu.Usuario)?.Value is { Length: > 0 } usuario)
            contexto["usuarioId"] = usuario;
        return contexto;
    }

    /// <summary>Prazo do aviso: passou dele, a resposta sai sem esperar (F13-09).</summary>
    internal static readonly TimeSpan PrazoDoAviso = TimeSpan.FromSeconds(3);

    /// <summary>Aviso em #alertas pela impressão digital do erro (F13-05), sem a mensagem da exceção.</summary>
    private static async Task AvisarAsync(HttpContext http, Exception excecao, Dictionary<string, object?> contexto, ILogger logger)
    {
        if (http.RequestServices.GetService<IAvisosDeErro>() is not { } avisos)
            return;
        var versao = http.RequestServices.GetRequiredService<IConfiguration>()["App:Versao"] is { Length: > 0 } v ? v : "dev";
        var erro = new ErroInesperado(
            FormatadorLogJson.ServicoApi, excecao.GetType().FullName ?? excecao.GetType().Name, QuadroDoAcordu.De(excecao),
            $"{contexto["metodo"]} {contexto["rota"]}", versao);
        using var prazo = new CancellationTokenSource(PrazoDoAviso);
        try
        {
            await avisos.AvisarAsync(erro, prazo.Token).WaitAsync(prazo.Token);
        }
        catch (OperationCanceledException)
        {
            AvisoAtrasado(logger);
        }
    }

    internal static ProblemDetails Classificar(Exception? excecao) => excecao switch
    {
        TransicaoInvalidaException e => Problema(422, "transicao-invalida", "Transição de status inválida", e.Message),
        PeriodoFechadoException e => Problema(422, "periodo-fechado", "Período fechado", e.Message),
        ContratoIncoerenteException e => Problema(422, "contrato-incoerente", "Contrato incoerente", e.Message),
        AnexoInvalidoException e => Problema(422, "anexo-invalido", "Anexo inválido", e.Message),
        ProjetoDeOutroClienteException e => Problema(422, "projeto-de-outro-cliente", "Projeto de outro cliente", e.Message),
        ProjetoEncerradoException e => Problema(422, "projeto-encerrado", "Projeto encerrado", e.Message),
        SolicitacaoEncerradaException e => Problema(409, "solicitacao-encerrada", "Solicitação encerrada", e.Message),
        ArquivoGrandeDemaisException e => Problema(413, "anexo-grande-demais", "Anexo acima do limite", e.Message),
        BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } => Problema(
            413, "anexo-grande-demais", "Anexo acima do limite", "O corpo da requisição passa do limite de anexos."),
        DominioException e => Problema(422, "validacao", "Regra de negócio violada", e.Message),
        NaoEncontradoException e => Problema(404, "nao-encontrado", "Não encontrado", e.Message),
        AcessoNegadoException e => Problema(403, "sem-permissao", "Sem permissão", e.Message),
        EmailNaoConfirmadoException e => Problema(422, "email-nao-confirmado", "E-mail não confirmado", e.Message),
        CronometroAtivoException e => Problema(409, "cronometro-ativo", "Cronômetro já ativo", e.Message),
        ListaDesatualizadaException e => Problema(409, "conflito-versao", "Versão divergente", e.Message),
        DbUpdateConcurrencyException => Problema(
            409, "conflito-versao", "Versão divergente", "O recurso foi alterado por outra requisição. Recarregue e tente de novo."),
        BadHttpRequestException => Problema(400, "validacao", "Requisição inválida", "Corpo ou parâmetros da requisição inválidos."),
        ArgumentException e => Validacao(e),
        _ => Problema(500, "erro-interno", "Erro interno", null),
    };

    private static ProblemDetails Problema(int status, string tipo, string titulo, string? detalhe) =>
        new() { Status = status, Type = BaseTipos + tipo, Title = titulo, Detail = detalhe };

    /// <summary>Mensagem do domínio sem o sufixo "(Parameter 'x')" do .NET, e o campo em <c>errors</c>.</summary>
    private static ProblemDetails Validacao(ArgumentException e)
    {
        var mensagem = e.Message.Split('\n')[0].Trim();
        if (e.ParamName is { } campo)
            mensagem = mensagem.Replace($" (Parameter '{campo}')", "", StringComparison.Ordinal);

        var problema = Problema(400, "validacao", "Dados inválidos", mensagem);
        if (e.ParamName is { } nome)
            problema.Extensions["errors"] = new Dictionary<string, string[]> { [nome] = [mensagem] };
        return problema;
    }

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Aviso do erro inesperado passou do prazo; a resposta saiu sem ele.")]
    private static partial void AvisoAtrasado(ILogger logger);

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Erro inesperado ao processar a requisição.")]
    private static partial void RegistrarErroInesperado(ILogger logger, Exception? excecao);
}
