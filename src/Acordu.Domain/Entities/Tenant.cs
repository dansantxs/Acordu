using Acordu.Domain.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;
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

    private Tenant(Guid id, string nome, DateTimeOffset criadoEm, Guid criadoPorUsuarioId, DateTimeOffset fimDoTeste)
    {
        Id = id;
        Nome = nome;
        CriadoEm = criadoEm;
        CriadoPorUsuarioId = criadoPorUsuarioId;
        DiasFechamentoAutomatico = DiasFechamentoPadrao;
        Situacao = SituacaoEmpresa.Teste;
        FimDoTeste = fimDoTeste;
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
    /// do criador ficou confirmado. Nunca volta a nulo (spec do F9, decisão 2). Gravada pela Infrastructure na
    /// confirmação, por atualização direta: a rota é pública e não tem o contexto da empresa (convenções §1).
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

    /// <summary>Situação comercial (RF-EMP-01, modelo §4.11.3). Muda só pelas transições de domínio.</summary>
    public SituacaoEmpresa Situacao { get; private set; }

    /// <summary>Instante em que o teste acaba; nulo em empresa que nunca esteve em teste (as anteriores ao F14).</summary>
    public DateTimeOffset? FimDoTeste { get; private set; }

    /// <summary>
    /// Início do só leitura atual (vencimento ou cancelamento); nulo enquanto a empresa grava. A data prevista do expurgo
    /// é calculada a partir dele (<see cref="ExpurgoPrevistoEm"/>), não gravada.
    /// </summary>
    public DateTimeOffset? SomenteLeituraDesde { get; private set; }

    /// <summary>Quando a empresa foi cancelada (pela Nebrys, ou pela migration do F14 nas anteriores a ele).</summary>
    public DateTimeOffset? CanceladaEm { get; private set; }

    /// <summary>VENCIDA ou CANCELADA: a empresa lê e não grava (RF-EMP-02).</summary>
    public bool SomenteLeitura => Situacao.SomenteLeitura();

    /// <summary>
    /// A empresa nasce em TESTE, com o fim do teste no instante do cadastro mais a duração (RF-EMP-01; a duração vem da
    /// configuração, padrão de 30 dias pela decisão 1 do G2 do F14).
    /// </summary>
    public static Tenant Criar(Guid id, string nome, DateTimeOffset agora, Guid criadoPorUsuarioId, TimeSpan duracaoDoTeste)
    {
        if (duracaoDoTeste <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duracaoDoTeste), duracaoDoTeste, "A duração do teste precisa ser positiva.");

        var criadoEm = agora.ToUniversalTime();
        return new(
            Ids.Obrigatorio(id, nameof(id)),
            Texto.Obrigatorio(nome, LimitesTexto.Nome, nameof(nome)),
            criadoEm,
            Ids.Obrigatorio(criadoPorUsuarioId, nameof(criadoPorUsuarioId)),
            criadoEm + duracaoDoTeste);
    }

    /// <summary>
    /// TESTE → VENCIDA (job diário, F14-03), só com o fim do teste já alcançado em <paramref name="agora"/>. O só leitura
    /// começa no instante do vencimento, que fica gravado como a data do vencimento. O fim do teste fica como estava.
    /// </summary>
    public void Vencer(DateTimeOffset agora)
    {
        if (Situacao != SituacaoEmpresa.Teste)
            throw new TransicaoInvalidaException("Só uma empresa em teste vence.");
        if (FimDoTeste is not { } fim || agora < fim)
            throw new TransicaoInvalidaException("O teste desta empresa ainda não acabou.");

        Situacao = SituacaoEmpresa.Vencida;
        SomenteLeituraDesde = agora.ToUniversalTime();
    }

    /// <summary>
    /// Data prevista do expurgo (RF-EMP-03): início do só leitura mais o prazo da configuração; nulo quando a empresa grava.
    /// </summary>
    public DateTimeOffset? ExpurgoPrevistoEm(TimeSpan prazoAteExpurgo) =>
        SomenteLeitura && SomenteLeituraDesde is { } desde ? desde + prazoAteExpurgo : null;

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
