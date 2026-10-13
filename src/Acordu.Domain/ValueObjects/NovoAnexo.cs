namespace Acordu.Domain.ValueObjects;

/// <summary>
/// Metadados de um arquivo já enviado ao object storage, prontos para virar <c>Anexo</c> de uma interação.
/// A validação contra a <see cref="PoliticaAnexos"/> acontece em <c>Interacao.Registrar</c>. <see cref="Id"/>: o Id que o
/// anexo terá, quando a chave no armazenamento já o usa (<c>{tenant}/{solicitacao}/{anexoId}</c>); nulo gera um novo.
/// </summary>
public sealed record NovoAnexo(string NomeArquivo, string TipoConteudo, long TamanhoBytes, string ChaveArmazenamento, Guid? Id = null);
