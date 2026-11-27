using Acordu.Domain.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;
using Acordu.Domain.Exceptions;
using Acordu.Domain.Services;
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

    /// <summary>Por onde a mensagem entrou (RF-SOL-14): a equipe só pelo ACORDU; o contato nunca por ele.</summary>
    public CanalMensagem Canal { get; private set; }

    public DateTimeOffset RegistradaEm { get; private set; }

    public IReadOnlyList<Anexo> Anexos => _anexos;

    /// <summary>
    /// Registra a interação. Exige texto ou ao menos um anexo; autor cliente só registra EXTERNA. Anexos são
    /// validados contra a <paramref name="politica"/> (<see cref="AnexoInvalidoException"/>). Menções (<c>@{id}</c>) só
    /// valem em interação INTERNA da equipe e emitem <see cref="PessoasMencionadas"/>; no texto do cliente não são lidas.
    /// O <paramref name="canal"/> é obrigatório (ADR 0016, item 6): da equipe, só <see cref="CanalMensagem.Acordu"/>; do
    /// contato, qualquer outro.
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
        CanalMensagem canal,
        IReadOnlyList<NovoAnexo> anexos,
        PoliticaAnexos politica,
        DateTimeOffset agora,
        bool pedeRetorno = false)
    {
        ArgumentNullException.ThrowIfNull(anexos);
        if (pedeRetorno && (autorEhCliente || visibilidade != Visibilidade.Externa))
            throw new ArgumentException("Só a resposta da equipe ao cliente pede retorno.", nameof(pedeRetorno));
        ArgumentNullException.ThrowIfNull(politica);
        if (!Enum.IsDefined(visibilidade))
            throw new ArgumentOutOfRangeException(nameof(visibilidade), visibilidade, "Visibilidade inválida.");
        if (autorEhCliente && visibilidade != Visibilidade.Externa)
            throw new ArgumentException("Interação do cliente é sempre EXTERNA.", nameof(visibilidade));
        if (!Enum.IsDefined(canal))
            throw new ArgumentOutOfRangeException(nameof(canal), canal, "Canal inválido.");
        if (autorEhCliente && canal == CanalMensagem.Acordu)
            throw new ArgumentException("Mensagem do contato nunca entra pelo canal Acordu.", nameof(canal));
        if (!autorEhCliente && canal != CanalMensagem.Acordu)
            throw new ArgumentException("A equipe escreve só pelo canal Acordu.", nameof(canal));
        if (AutoresDoSistema.Eh(autorId))
            throw new ArgumentException("O autor do sistema só escreve a nota do sistema.", nameof(autorId));

        var textoValido = Common.Texto.Opcional(texto, LimitesTexto.TextoInteracao, nameof(texto));
        if (textoValido is null && anexos.Count == 0)
            throw new ArgumentException("A interação precisa de texto ou de ao menos um anexo.", nameof(texto));
        if (anexos.Count > politica.MaximoPorInteracao)
            throw new AnexoInvalidoException($"No máximo {politica.MaximoPorInteracao} anexos por interação.");
        IReadOnlyList<Guid> mencionados = autorEhCliente ? [] : Mencoes.Extrair(textoValido, autorId);
        if (mencionados.Count > 0 && visibilidade != Visibilidade.Interna)
            throw new ArgumentException("Menção só vale em interação interna: o cliente veria o nome da equipe.", nameof(texto));

        var interacao = new Interacao(id, tenantId, clienteId)
        {
            SolicitacaoId = Ids.Obrigatorio(solicitacaoId, nameof(solicitacaoId)),
            AutorId = Ids.Obrigatorio(autorId, nameof(autorId)),
            AutorEhCliente = autorEhCliente,
            Texto = textoValido,
            Visibilidade = visibilidade,
            Canal = canal,
            RegistradaEm = agora.ToUniversalTime(),
        };
        foreach (var anexo in anexos)
            interacao._anexos.Add(Anexo.Criar(anexo, politica));

        interacao.Registrar(new InteracaoRegistrada(
            id, tenantId, clienteId, interacao.SolicitacaoId, interacao.AutorId, autorEhCliente, visibilidade,
            interacao.RegistradaEm, pedeRetorno));
        if (mencionados.Count > 0)
        {
            interacao.Registrar(new PessoasMencionadas(
                tenantId, AlvoColaboracao.Solicitacao, interacao.SolicitacaoId, id, interacao.AutorId, mencionados));
        }

        return interacao;
    }

    /// <summary>
    /// A <b>nota do sistema</b> (design do F15, D15; ADR 0016, item 10): o que o próprio Acordu escreve na conversa (hoje,
    /// os anexos recusados de um e-mail recebido, F15-15), sempre INTERNA, com um autor de <see cref="AutoresDoSistema"/>
    /// (nunca uma pessoa da equipe) e o <paramref name="canal"/> da entrada que a gerou: a exceção única à regra "a equipe
    /// só pelo canal Acordu" (D8). Não registra <see cref="InteracaoRegistrada"/>: ninguém da equipe agiu, então nada de
    /// aviso (ao contato, RNF-SEG-03, F15-98; nem aos seguidores), de feed, de seguimento automático nem de primeira
    /// resposta. Sem anexos nem menções.
    /// </summary>
    public static Interacao RegistrarNotaDoSistema(
        Guid id,
        Guid tenantId,
        Guid clienteId,
        Guid solicitacaoId,
        Guid autorId,
        string texto,
        CanalMensagem canal,
        DateTimeOffset agora)
    {
        if (!AutoresDoSistema.Eh(autorId))
            throw new ArgumentException("A nota do sistema só tem autor do sistema.", nameof(autorId));
        if (!Enum.IsDefined(canal))
            throw new ArgumentOutOfRangeException(nameof(canal), canal, "Canal inválido.");
        if (canal == CanalMensagem.Acordu)
            throw new ArgumentException("A nota do sistema leva o canal da entrada que a gerou.", nameof(canal));

        return new Interacao(id, tenantId, clienteId)
        {
            SolicitacaoId = Ids.Obrigatorio(solicitacaoId, nameof(solicitacaoId)),
            AutorId = autorId,
            AutorEhCliente = false,
            Texto = Common.Texto.Obrigatorio(texto, LimitesTexto.TextoInteracao, nameof(texto)),
            Visibilidade = Visibilidade.Interna,
            Canal = canal,
            RegistradaEm = agora.ToUniversalTime(),
        };
    }
}
