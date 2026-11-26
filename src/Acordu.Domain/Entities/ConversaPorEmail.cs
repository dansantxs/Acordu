using System.Buffers;
using System.Security.Cryptography;
using Acordu.Domain.Common;

namespace Acordu.Domain.Entities;

/// <summary>
/// Endereço da conversa de uma solicitação por e-mail (RF-NOT-06, RF-SOL-11; design do F15, D3 e D9; ADR 0016, item 3):
/// os avisos ao contato respondem para <c>{código da empresa}+{Token}@{domínio de entrada}</c>, e a resposta é reconhecida
/// só pelo destinatário (o token procurado dentro da empresa do código). Uma por solicitação, criada na abertura por
/// e-mail ou, nas outras origens, no primeiro aviso com o recebimento ligado.
/// <para>
/// O token é identificador de roteamento, não credencial (convenções §1): fica em claro, com índice único na empresa,
/// porque vai em todo aviso; entrar na conversa exige ainda remetente contato ativo do cliente e autenticidade.
/// </para>
/// </summary>
public sealed class ConversaPorEmail : RaizComCliente
{
    /// <summary>16 símbolos do alfabeto do código de entrada (~78 bits).</summary>
    public const int TamanhoToken = 16;

    /// <summary>Teto do <c>Message-ID</c> e do assunto guardados (o limite de linha da RFC 5322).</summary>
    public const int TamanhoCabecalho = 998;

    private static readonly SearchValues<char> Simbolos = SearchValues.Create(CodigoDeEntrada.Alfabeto);

    private ConversaPorEmail(Guid id, Guid tenantId, Guid clienteId)
        : base(id, tenantId, clienteId)
    {
        Token = null!;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private ConversaPorEmail()
    {
        Token = null!;
    }

    public Guid SolicitacaoId { get; private set; }

    /// <summary>Parte do endereço da conversa depois do "+"; sorteado com <see cref="RandomNumberGenerator"/>.</summary>
    public string Token { get; private set; }

    /// <summary><c>Message-ID</c> do e-mail que abriu a solicitação (fio dos avisos, D4); nulo fora da origem EMAIL.</summary>
    public string? MessageIdOrigem { get; private set; }

    /// <summary>Assunto do e-mail que abriu a solicitação (D4); nulo fora da origem EMAIL.</summary>
    public string? AssuntoOrigem { get; private set; }

    public DateTimeOffset CriadaEm { get; private set; }

    /// <summary>A conversa de uma solicitação de origem INTERNA ou PORTAL, no primeiro aviso com o recebimento ligado.</summary>
    public static ConversaPorEmail Criar(Guid id, Guid tenantId, Guid clienteId, Guid solicitacaoId, DateTimeOffset agora) =>
        new(id, tenantId, clienteId)
        {
            SolicitacaoId = Ids.Obrigatorio(solicitacaoId, nameof(solicitacaoId)),
            Token = SortearToken(),
            CriadaEm = agora.ToUniversalTime(),
        };

    /// <summary>
    /// A conversa de uma solicitação aberta por e-mail (F15f), com o <c>Message-ID</c> e o assunto do e-mail dela, para os
    /// avisos seguirem no mesmo fio (D4). O assunto pode ser vazio (e-mail sem assunto); o <c>Message-ID</c>, não.
    /// </summary>
    public static ConversaPorEmail DaAberturaPorEmail(
        Guid id, Guid tenantId, Guid clienteId, Guid solicitacaoId, string messageId, string? assunto, DateTimeOffset agora)
    {
        var conversa = Criar(id, tenantId, clienteId, solicitacaoId, agora);
        conversa.MessageIdOrigem = Texto.Obrigatorio(messageId, TamanhoCabecalho, nameof(messageId));
        conversa.AssuntoOrigem = Texto.Opcional(assunto, TamanhoCabecalho, nameof(assunto)) ?? string.Empty;
        return conversa;
    }

    /// <summary>Um token novo, uniforme no alfabeto do código de entrada (sem viés de módulo).</summary>
    public static string SortearToken() => RandomNumberGenerator.GetString(CodigoDeEntrada.Alfabeto, TamanhoToken);

    /// <summary>Tem o tamanho e só símbolos do alfabeto (a entrada, no F15i, recusa o resto sem ir ao banco).</summary>
    public static bool TokenValido(string? token) =>
        token is { Length: TamanhoToken } && !token.AsSpan().ContainsAnyExcept(Simbolos);
}
