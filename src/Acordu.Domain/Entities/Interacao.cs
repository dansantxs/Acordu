using Acordu.Domain.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.Entities;

/// <summary>
/// Comentário com anexos na linha do tempo de uma solicitação (modelo de domínio §4.5). Agregado próprio porque cresce
/// sem limite; imutável depois de registrado no v1.
/// </summary>
public sealed class Interacao : RaizComCliente
{
    private readonly List<Anexo> _anexos = [];

    private Interacao(Guid id, Guid tenantId, Guid clienteId)
        : base(id, tenantId, clienteId)
    {
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private Interacao()
    {
    }

    public Guid SolicitacaoId { get; private set; }

    public Guid AutorId { get; private set; }

    public bool AutorEhCliente { get; private set; }

    public string? Texto { get; private set; }

    public Visibilidade Visibilidade { get; private set; }

    public DateTimeOffset RegistradaEm { get; private set; }

    public IReadOnlyList<Anexo> Anexos => _anexos;

    /// <summary>
    /// Registra a interação. Exige texto ou ao menos um anexo; autor cliente só registra EXTERNA. Anexos são
    /// validados contra a <paramref name="politica"/> (<see cref="AnexoInvalidoException"/>).
    /// </summary>
    public static Interacao Registrar(
        Guid id,
        Guid tenantId,
        Guid clienteId,
        Guid solicitacaoId,
        Guid autorId,
        bool autorEhCliente,
        string? texto,
        Visibilidade visibilidade,
        IReadOnlyList<NovoAnexo> anexos,
        PoliticaAnexos politica,
        DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(anexos);
        ArgumentNullException.ThrowIfNull(politica);
        if (!Enum.IsDefined(visibilidade))
            throw new ArgumentOutOfRangeException(nameof(visibilidade), visibilidade, "Visibilidade inválida.");
        if (autorEhCliente && visibilidade != Visibilidade.Externa)
            throw new ArgumentException("Interação do cliente é sempre EXTERNA.", nameof(visibilidade));

        var textoValido = Common.Texto.Opcional(texto, LimitesTexto.TextoInteracao, nameof(texto));
        if (textoValido is null && anexos.Count == 0)
            throw new ArgumentException("A interação precisa de texto ou de ao menos um anexo.", nameof(texto));
        if (anexos.Count > politica.MaximoPorInteracao)
            throw new AnexoInvalidoException($"No máximo {politica.MaximoPorInteracao} anexos por interação.");

        var interacao = new Interacao(id, tenantId, clienteId)
        {
            SolicitacaoId = Ids.Obrigatorio(solicitacaoId, nameof(solicitacaoId)),
            AutorId = Ids.Obrigatorio(autorId, nameof(autorId)),
            AutorEhCliente = autorEhCliente,
            Texto = textoValido,
            Visibilidade = visibilidade,
            RegistradaEm = agora.ToUniversalTime(),
        };
        foreach (var anexo in anexos)
            interacao._anexos.Add(CriarAnexo(anexo, politica));

        interacao.Registrar(new InteracaoRegistrada(
            id, tenantId, clienteId, interacao.SolicitacaoId, interacao.AutorId, autorEhCliente, visibilidade,
            interacao.RegistradaEm));
        return interacao;
    }

    private static Anexo CriarAnexo(NovoAnexo anexo, PoliticaAnexos politica)
    {
        ArgumentNullException.ThrowIfNull(anexo);
        var nome = Common.Texto.Obrigatorio(anexo.NomeArquivo, LimitesTexto.NomeArquivo, nameof(anexo));
        if (nome.IndexOfAny(['/', '\\']) >= 0 || nome is "." or "..")
            throw new AnexoInvalidoException($"Nome de arquivo inválido: {nome}.");
        if (string.IsNullOrWhiteSpace(anexo.ChaveArmazenamento))
            throw new ArgumentException("A chave de armazenamento é obrigatória.", nameof(anexo));
        if (anexo.TamanhoBytes <= 0 || anexo.TamanhoBytes > politica.TamanhoMaximoBytes)
            throw new AnexoInvalidoException($"O arquivo {nome} excede o tamanho permitido ou está vazio.");
        if (string.IsNullOrWhiteSpace(anexo.TipoConteudo) || !politica.Permite(anexo.TipoConteudo))
            throw new AnexoInvalidoException($"O tipo do arquivo {nome} não é permitido.");

        return new Anexo(
            anexo.Id ?? Guid.CreateVersion7(), nome, anexo.TipoConteudo.Trim().ToLowerInvariant(), anexo.TamanhoBytes,
            anexo.ChaveArmazenamento);
    }
}
