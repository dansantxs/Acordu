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
            ErroInesperado(logger, excecao);
        }

        http.Response.StatusCode = problema.Status ?? StatusCodes.Status500InternalServerError;
        var servico = http.RequestServices.GetRequiredService<IProblemDetailsService>();
        await servico.WriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problema, Exception = excecao });
    }

    internal static ProblemDetails Classificar(Exception? excecao) => excecao switch
    {
        TransicaoInvalidaException e => Problema(422, "transicao-invalida", "Transição de status inválida", e.Message),
        PeriodoFechadoException e => Problema(422, "periodo-fechado", "Período fechado", e.Message),
        ContratoIncoerenteException e => Problema(422, "contrato-incoerente", "Contrato incoerente", e.Message),
        DominioException e => Problema(422, "validacao", "Regra de negócio violada", e.Message),
        NaoEncontradoException e => Problema(404, "nao-encontrado", "Não encontrado", e.Message),
        AcessoNegadoException e => Problema(403, "sem-permissao", "Sem permissão", e.Message),
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

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Erro inesperado ao processar a requisição.")]
    private static partial void ErroInesperado(ILogger logger, Exception? excecao);
}
