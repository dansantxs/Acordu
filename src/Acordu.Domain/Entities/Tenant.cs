using Acordu.Domain.Common;

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

    private Tenant(Guid id, string nome, DateTimeOffset criadoEm)
    {
        Id = id;
        Nome = nome;
        CriadoEm = criadoEm;
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

    /// <summary>Nome mostrado aos clientes no portal (RF-POR-09); nulo usa o <see cref="Nome"/>.</summary>
    public string? NomePortal { get; private set; }

    /// <summary>Dias em RESOLVIDA sem manifestação do cliente até o fechamento automático (RF-SOL-09).</summary>
    public int DiasFechamentoAutomatico { get; private set; }

    public static Tenant Criar(Guid id, string nome, DateTimeOffset agora) =>
        new(Ids.Obrigatorio(id, nameof(id)), Texto.Obrigatorio(nome, LimitesTexto.Nome, nameof(nome)), agora.ToUniversalTime());

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
}
