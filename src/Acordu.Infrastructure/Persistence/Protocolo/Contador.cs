namespace Acordu.Infrastructure.Persistence.Protocolo;

/// <summary>Contador sequencial por tenant (chave: tenant + nome). Lido e incrementado só por SQL atômico.</summary>
public sealed class Contador
{
    public Contador(Guid tenantId, string nome, int valor)
    {
        TenantId = tenantId;
        Nome = nome;
        Valor = valor;
    }

    public Guid TenantId { get; private set; }

    public string Nome { get; private set; }

    public int Valor { get; private set; }
}
