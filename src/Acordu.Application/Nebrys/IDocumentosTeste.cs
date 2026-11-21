using Acordu.Domain.Entities;

namespace Acordu.Application.Nebrys;

/// <summary>
/// Registro global dos documentos que já tiveram teste (RF-AUT-09, design D8), visto pela liberação da área da Nebrys
/// (F14-71, F14-108). A implementação calcula o HMAC do documento digitado e lê o registro por ele: o documento em claro
/// não é guardado, nem vai para a trilha.
/// </summary>
public interface IDocumentosTeste
{
    /// <summary>O registro do documento (já normalizado), rastreado para a gravação; nulo se ele nunca teve teste.</summary>
    Task<DocumentoTeste?> ObterAsync(string documentoNormalizado, CancellationToken cancellationToken = default);
}
