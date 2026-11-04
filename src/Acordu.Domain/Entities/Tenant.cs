using Acordu.Domain.Common;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.Entities;

/// <summary>
/// O prestador de serviço (segurança §2). É a própria fronteira do isolamento: o <see cref="Id"/> é o <c>TenantId</c>
/// de todo o resto. Não herda <see cref="EntidadeBase"/>, que exigiria um TenantId separado e redundante.
/// </summary>
public sealed class Tenant
{
    public const int DiasFechamentoPadrao = 5;
    public const int DiasFechamentoMinimo = 1;
    public const int DiasFechamentoMaximo = 60;

    private Tenant(Guid id, string nome, DateTimeOffset criadoEm, Guid criadoPorUsuarioId)
    {
        Id = id;
        Nome = nome;
        CriadoEm = criadoEm;
        CriadoPorUsuarioId = criadoPorUsuarioId;
        DiasFechamentoAutomatico = DiasFechamentoPadrao;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private Tenant()
    {
        Nome = null!;
    }

    public Guid Id { get; private set; }

    /// <summary>Nome da empresa do prestador, exibido para a equipe e no portal.</summary>
    public string Nome { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    /// <summary>
    /// Quem registrou a empresa (RF-AUT-06, F9): só a confirmação do e-mail dele libera o envio a terceiros. Nulo só em
    /// empresa anterior ao F9 sem Dono (a migration preenche com o Dono mais antigo).
    /// </summary>
    public Guid? CriadoPorUsuarioId { get; private set; }

    /// <summary>
    /// Quando a empresa passou a poder enviar e-mail a terceiros (convites, links, avisos): a primeira vez que o e-mail
    /// do criador ficou confirmado. Nunca volta a nulo (spec do F9, decisão 2).
    /// </summary>
    public DateTimeOffset? EnvioLiberadoEm { get; private set; }

    public bool EnvioLiberado => EnvioLiberadoEm is not null;

    /// <summary>Nome mostrado aos clientes no portal (RF-POR-09); nulo usa o <see cref="Nome"/>.</summary>
    public string? NomePortal { get; private set; }

    /// <summary>Dias em RESOLVIDA sem manifestação do cliente até o fechamento automático (RF-SOL-09).</summary>
    public int DiasFechamentoAutomatico { get; private set; }

    /// <summary>Chave do logotipo do portal no object storage (RF-POR-09); nulo = sem logotipo.</summary>
    public string? LogoChave { get; private set; }

    public const int TamanhoLogoChave = 512;

    /// <summary>
    /// Endereço que recebe as respostas dos e-mails ao contato (Reply-To; pedido de 16/10). O "De" continua sendo o
    /// endereço do Acordu; nulo = responder vai para o endereço sem caixa.
    /// </summary>
    public string? ResponderPara { get; private set; }

    /// <summary>Prazos sugeridos no cadastro de contrato; nulo = <see cref="PoliticaSla.Sugerida"/>.</summary>
    public PoliticaSla? SlaPadrao { get; private set; }

    /// <summary>A política que o cadastro de contrato sugere: a da empresa ou a do sistema.</summary>
    public PoliticaSla SlaSugerido => SlaPadrao ?? PoliticaSla.Sugerida;

    public static Tenant Criar(Guid id, string nome, DateTimeOffset agora, Guid criadoPorUsuarioId) =>
        new(
            Ids.Obrigatorio(id, nameof(id)),
            Texto.Obrigatorio(nome, LimitesTexto.Nome, nameof(nome)),
            agora.ToUniversalTime(),
            Ids.Obrigatorio(criadoPorUsuarioId, nameof(criadoPorUsuarioId)));

    /// <summary>Libera o envio a terceiros; idempotente (a primeira data fica).</summary>
    public void LiberarEnvio(DateTimeOffset agora) => EnvioLiberadoEm ??= agora.ToUniversalTime();

    public void Renomear(string nome) => Nome = Texto.Obrigatorio(nome, LimitesTexto.Nome, nameof(nome));

    public void ConfigurarPortal(string? nomePortal, int diasFechamentoAutomatico)
    {
        if (diasFechamentoAutomatico is < DiasFechamentoMinimo or > DiasFechamentoMaximo)
        {
            throw new ArgumentOutOfRangeException(
                nameof(diasFechamentoAutomatico),
                diasFechamentoAutomatico,
                $"O fechamento automático vai de {DiasFechamentoMinimo} a {DiasFechamentoMaximo} dias.");
        }

        NomePortal = Texto.Opcional(nomePortal, LimitesTexto.Nome, nameof(nomePortal));
        DiasFechamentoAutomatico = diasFechamentoAutomatico;
    }

    /// <summary>Nome da empresa, endereço de resposta (vazio = nenhum) e SLA sugerido nos contratos novos.</summary>
    public void ConfigurarEmpresa(string nome, string? responderPara, PoliticaSla slaPadrao)
    {
        ArgumentNullException.ThrowIfNull(slaPadrao);
        var novoNome = Texto.Obrigatorio(nome, LimitesTexto.Nome, nameof(nome));
        var endereco = string.IsNullOrWhiteSpace(responderPara) ? null : Email.Normalizar(responderPara, nameof(responderPara));
        Nome = novoNome;
        ResponderPara = endereco;
        SlaPadrao = slaPadrao;
    }

    /// <summary>Troca (ou remove, com nulo) o logotipo; o arquivo já está no storage.</summary>
    public void DefinirLogo(string? chave) => LogoChave = Texto.Opcional(chave, TamanhoLogoChave, nameof(chave));
}
