using Acordu.Domain.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.Entities;

/// <summary>
/// Um e-mail de remetente não identificado na triagem da empresa (RF-SOL-15; design do F15, D10; ADR 0016, item 4;
/// F15-70): remetente, assunto, o texto (como na abertura, F15-13), o instante da recepção, o <see cref="Motivo"/>, os
/// clientes possíveis (F15-24) e os anexos dentro da política (os binários no storage, na chave da triagem), com os
/// recusados e o motivo de cada um. Raiz por tenant, <b>sem cliente</b> (ADR 0002: o remetente pode nem ser contato; o
/// contato do portal nunca o vê). O item nunca é editado: nasce na chegada e sai inteiro numa das ações da equipe
/// (converter, juntar, descartar; F15k a F15m) ou no expurgo do diário.
/// </summary>
public sealed class ItemTriagemEmail : EntidadeBase
{
    /// <summary>Teto do assunto guardado (o limite de linha da RFC 5322).</summary>
    public const int TamanhoAssunto = 998;

    private readonly List<Anexo> _anexos = [];
    private List<Guid> _clientesPossiveis = [];

    private ItemTriagemEmail(Guid id, Guid tenantId)
        : base(id, tenantId)
    {
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private ItemTriagemEmail()
    {
    }

    /// <summary>O e-mail do <c>From</c>, normalizado como o dos contatos; nulo quando o <c>From</c> não tem um endereço só.</summary>
    public string? RemetenteEmail { get; private set; }

    /// <summary>O nome que o <c>From</c> traz ao lado do endereço; nulo sem ele.</summary>
    public string? RemetenteNome { get; private set; }

    /// <summary>O assunto como veio, numa linha; nulo sem assunto.</summary>
    public string? Assunto { get; private set; }

    /// <summary>O corpo só como texto (o HTML já convertido); nulo sem corpo.</summary>
    public string? Texto { get; private set; }

    /// <summary>Instante da recepção (o do servidor, nunca o <c>Date</c> do e-mail; design D14).</summary>
    public DateTimeOffset RecebidoEm { get; private set; }

    public MotivoTriagem Motivo { get; private set; }

    /// <summary>Os clientes dos contatos ativos com o e-mail do remetente (só com <see cref="MotivoTriagem.MaisDeUmCliente"/>).</summary>
    public IReadOnlyList<Guid> ClientesPossiveis => _clientesPossiveis;

    /// <summary>Os anexos válidos, na ordem do MIME, até o máximo por interação (os binários na chave da triagem).</summary>
    public IReadOnlyList<Anexo> Anexos => _anexos;

    /// <summary>Os anexos que ficaram de fora, com o motivo (D14); nulo quando todos entraram.</summary>
    public AnexosRecusados? Recusados { get; private set; }

    /// <summary>
    /// O item na chegada do e-mail (F15-70). Os <paramref name="clientesPossiveis"/> (ao menos dois, sem repetir)
    /// acompanham só o motivo <see cref="MotivoTriagem.MaisDeUmCliente"/>; os <paramref name="anexos"/>, já enviados ao
    /// storage, são validados contra a <paramref name="politica"/> (<see cref="AnexoInvalidoException"/>).
    /// </summary>
    public static ItemTriagemEmail Receber(
        Guid id,
        Guid tenantId,
        string? remetenteEmail,
        string? remetenteNome,
        string? assunto,
        string? texto,
        MotivoTriagem motivo,
        IReadOnlyList<Guid> clientesPossiveis,
        IReadOnlyList<NovoAnexo> anexos,
        AnexosRecusados? recusados,
        PoliticaAnexos politica,
        DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(clientesPossiveis);
        ArgumentNullException.ThrowIfNull(anexos);
        ArgumentNullException.ThrowIfNull(politica);
        if (!Enum.IsDefined(motivo))
            throw new ArgumentOutOfRangeException(nameof(motivo), motivo, "Motivo desconhecido.");
        if (clientesPossiveis.Contains(Guid.Empty))
            throw new ArgumentException("O Id não pode ser vazio.", nameof(clientesPossiveis));
        var clientes = clientesPossiveis.Distinct().ToList();
        if (clientes.Count != clientesPossiveis.Count)
            throw new ArgumentException("Cliente possível repetido.", nameof(clientesPossiveis));
        if (motivo == MotivoTriagem.MaisDeUmCliente ? clientes.Count < 2 : clientes.Count > 0)
            throw new ArgumentException("Os clientes possíveis (dois ou mais) acompanham só o contato em mais de um cliente.", nameof(clientesPossiveis));
        if (anexos.Count > politica.MaximoPorInteracao)
            throw new AnexoInvalidoException($"No máximo {politica.MaximoPorInteracao} anexos por item.");

        var item = new ItemTriagemEmail(id, tenantId)
        {
            RemetenteEmail = remetenteEmail is null ? null : Email.Normalizar(remetenteEmail, nameof(remetenteEmail)),
            RemetenteNome = Common.Texto.Opcional(remetenteNome, LimitesTexto.Nome, nameof(remetenteNome)),
            Assunto = Common.Texto.Opcional(assunto, TamanhoAssunto, nameof(assunto)),
            Texto = Common.Texto.Opcional(texto, LimitesTexto.Descricao, nameof(texto)),
            RecebidoEm = agora.ToUniversalTime(),
            Motivo = motivo,
            _clientesPossiveis = clientes,
            Recusados = recusados,
        };
        foreach (var anexo in anexos)
            item._anexos.Add(Anexo.Criar(anexo, politica));
        return item;
    }
}
