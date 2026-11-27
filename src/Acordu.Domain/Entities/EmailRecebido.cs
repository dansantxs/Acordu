using System.Buffers;
using Acordu.Domain.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.Entities;

/// <summary>
/// Registro de entrada de um e-mail aceito para uma empresa (RF-SOL-11; design do F15, D6; ADR 0016, item 8), sem o
/// conteúdo: a chave de idempotência (<see cref="ChaveEntrada"/>, única na empresa), o hash do remetente, o instante da
/// recepção e o <see cref="Resultado"/>; da abertura, a solicitação e os anexos recusados (F15g); da triagem, o item (F15h). Serve à idempotência (a repetição da mesma entrega esbarra no índice único) e às
/// contagens dos limites (F15n). Nada do assunto, do texto nem dos endereços em claro. Retenção curta, apagada pelo
/// diário (F15e). Raiz por tenant, sem cliente: o remetente pode nem ser contato.
/// </summary>
public sealed class EmailRecebido : EntidadeBase
{
    /// <summary>SHA-256 em hex minúsculo.</summary>
    public const int TamanhoHash = 64;

    private static readonly SearchValues<char> Hexadecimais = SearchValues.Create("0123456789abcdef");

    private EmailRecebido(Guid id, Guid tenantId)
        : base(id, tenantId)
    {
        ChaveEntrada = null!;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private EmailRecebido()
    {
        ChaveEntrada = null!;
    }

    /// <summary>SHA-256 (hex) do <c>Message-ID</c> e do destino, ou do MIME inteiro e do destino sem <c>Message-ID</c>.</summary>
    public string ChaveEntrada { get; private set; }

    /// <summary>SHA-256 (hex) do e-mail do remetente normalizado; nulo quando o <c>From</c> não tem um endereço só.</summary>
    public string? RemetenteHash { get; private set; }

    /// <summary>Instante da recepção (o do servidor, nunca o <c>Date</c> do e-mail; design D14).</summary>
    public DateTimeOffset RecebidoEm { get; private set; }

    public ResultadoEntradaEmail Resultado { get; private set; }

    /// <summary>A solicitação que o e-mail abriu (resultado SOLICITACAO); nula nos outros.</summary>
    public Guid? SolicitacaoId { get; private set; }

    /// <summary>
    /// Os anexos que não entraram, com o motivo (D14, F15-15): o aviso de recebida os lê daqui. Só o nome do arquivo e o
    /// motivo; nulo quando nenhum foi recusado.
    /// </summary>
    public AnexosRecusados? Recusados { get; private set; }

    /// <summary>O item que o e-mail criou na triagem (resultado TRIAGEM, F15h); nulo nos outros.</summary>
    public Guid? ItemTriagemId { get; private set; }

    /// <param name="solicitacaoId">Obrigatório com o resultado SOLICITACAO e proibido nos outros.</param>
    /// <param name="recusados">Só com uma solicitação aberta (os outros resultados não guardam anexos aqui).</param>
    /// <param name="itemTriagemId">Obrigatório com o resultado TRIAGEM e proibido nos outros.</param>
    public static EmailRecebido Registrar(
        Guid id,
        Guid tenantId,
        string chaveEntrada,
        string? remetenteHash,
        ResultadoEntradaEmail resultado,
        DateTimeOffset agora,
        Guid? solicitacaoId = null,
        AnexosRecusados? recusados = null,
        Guid? itemTriagemId = null)
    {
        if (!Enum.IsDefined(resultado))
            throw new ArgumentOutOfRangeException(nameof(resultado), resultado, "Resultado desconhecido.");
        if ((resultado == ResultadoEntradaEmail.Solicitacao) != solicitacaoId.HasValue)
            throw new ArgumentException("A solicitação acompanha só o resultado SOLICITACAO.", nameof(solicitacaoId));
        if (recusados is not null && resultado != ResultadoEntradaEmail.Solicitacao)
            throw new ArgumentException("Os anexos recusados acompanham só a abertura.", nameof(recusados));
        if ((resultado == ResultadoEntradaEmail.Triagem) != itemTriagemId.HasValue)
            throw new ArgumentException("O item da triagem acompanha só o resultado TRIAGEM.", nameof(itemTriagemId));
        return new EmailRecebido(id, tenantId)
        {
            ChaveEntrada = Hash(chaveEntrada, nameof(chaveEntrada)),
            RemetenteHash = remetenteHash is null ? null : Hash(remetenteHash, nameof(remetenteHash)),
            Resultado = resultado,
            RecebidoEm = agora.ToUniversalTime(),
            SolicitacaoId = solicitacaoId is { } solicitacao ? Ids.Obrigatorio(solicitacao, nameof(solicitacaoId)) : null,
            Recusados = recusados,
            ItemTriagemId = itemTriagemId is { } item ? Ids.Obrigatorio(item, nameof(itemTriagemId)) : null,
        };
    }

    /// <summary>Hex minúsculo de 64 caracteres (o formato de <c>Convert.ToHexStringLower</c> de um SHA-256).</summary>
    public static bool HashValido(string? valor) =>
        valor is { Length: TamanhoHash } && !valor.AsSpan().ContainsAnyExcept(Hexadecimais);

    private static string Hash(string valor, string parametro) =>
        HashValido(valor) ? valor : throw new ArgumentException("Esperado um SHA-256 em hex minúsculo.", parametro);
}
