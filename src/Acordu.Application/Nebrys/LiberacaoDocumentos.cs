using Acordu.Application.Abstractions;
using Acordu.Application.Auditoria;
using Acordu.Application.Common;
using Acordu.Domain.Common;
using Acordu.Domain.Exceptions;

namespace Acordu.Application.Nebrys;

/// <summary>
/// Liberação de um CPF ou CNPJ para novo teste pela área da Nebrys (F14-71, F14-108; RF-AUT-09; design D8). A Nebrys
/// digita o documento; ele é normalizado como no cadastro, e o registro é achado pelo HMAC dele, inclusive o de uma
/// empresa já expurgada, da qual só ficou o registro. O documento não é guardado em lugar nenhum nem vai para a trilha:
/// ela leva só as datas da liberação, antes e depois. A liberação vale para um cadastro só (F14-59).
/// </summary>
public sealed class LiberacaoDocumentos
{
    /// <summary>Campo do documento no corpo da liberação.</summary>
    public const string CampoDocumento = "documento";

    /// <summary>Tipo do alvo na trilha: o documento não tem id (só o HMAC, que não vai para a trilha).</summary>
    private const string AlvoDocumento = "documento";

    private readonly IDocumentosTeste _documentos;
    private readonly ITrilhaDaOperacao _trilha;
    private readonly IUnitOfWork _unidade;
    private readonly TimeProvider _relogio;

    public LiberacaoDocumentos(IDocumentosTeste documentos, ITrilhaDaOperacao trilha, IUnitOfWork unidade, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(documentos);
        ArgumentNullException.ThrowIfNull(trilha);
        ArgumentNullException.ThrowIfNull(unidade);
        ArgumentNullException.ThrowIfNull(relogio);
        _documentos = documentos;
        _trilha = trilha;
        _unidade = unidade;
        _relogio = relogio;
    }

    /// <summary>
    /// Libera o documento. Vazio ou inválido → <c>400</c> no campo <c>documento</c>; documento que nunca teve teste, ou com
    /// uma liberação ainda não usada → <c>422 transicao-invalida</c>, sem gravar nada. Uma liberação já usada é substituída
    /// por uma nova.
    /// </summary>
    public async Task LiberarAsync(LiberacaoDocumentoRequest corpo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(corpo);
        var documento = Documento.Normalizar(corpo.Documento, CampoDocumento)
            ?? throw ErroDeCampo.Criar(CampoDocumento, "Informe o CPF ou CNPJ.");
        var registro = await _documentos.ObterAsync(documento, cancellationToken)
            ?? throw new TransicaoInvalidaException(
                "Este CPF ou CNPJ não teve teste no Acordu: o cadastro com ele já passa, sem liberação.");

        var antes = new { registro.LiberadoEm, registro.LiberacaoUsadaEm };
        registro.Liberar(_relogio.GetUtcNow());
        _trilha.Registrar(
            AcoesAuditoria.NebrysDocumentoLiberacao,
            AlvoDocumento,
            null,
            new { antes, depois = new { registro.LiberadoEm, registro.LiberacaoUsadaEm } });
        await _unidade.SalvarAsync(cancellationToken);
    }
}

/// <summary>Corpo do <c>POST /nebrys/documentos/liberacao</c>: o CPF ou CNPJ, com ou sem pontuação.</summary>
public sealed record LiberacaoDocumentoRequest(string? Documento);
