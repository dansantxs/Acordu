namespace Acordu.Infrastructure.Persistence.Outbox;

/// <summary>
/// Evento de domínio gravado na mesma transação do caso de uso, para efeitos externos depois do commit (modelo §7).
/// Não é dado de negócio: sem filtro global; só a Infrastructure lê e escreve.
/// </summary>
public sealed class MensagemOutbox
{
    public const int MaximoTentativas = 5;

    public MensagemOutbox(Guid id, Guid tenantId, string tipo, string conteudo, DateTimeOffset ocorridaEm, Guid? autorId = null)
    {
        Id = id;
        AutorId = autorId;
        TenantId = tenantId;
        Tipo = tipo;
        Conteudo = conteudo;
        OcorridaEm = ocorridaEm;
        ProximaTentativaEm = ocorridaEm;
    }

    public Guid Id { get; private set; }

    /// <summary>Ordem de gravação (identity), usada na entrega: Ids v7 do mesmo milissegundo não ordenam.</summary>
    public long Sequencia { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Nome curto do tipo do evento (ex.: <c>SolicitacaoAberta</c>).</summary>
    public string Tipo { get; private set; }

    /// <summary>Evento serializado em JSON.</summary>
    public string Conteudo { get; private set; }

    public DateTimeOffset OcorridaEm { get; private set; }

    /// <summary>Usuário da requisição que gravou o evento (nulo em job); chega aos manipuladores pelo <c>IOrigemDoEvento</c>.</summary>
    public Guid? AutorId { get; private set; }

    public DateTimeOffset? ProcessadaEm { get; private set; }

    public int Tentativas { get; private set; }

    public DateTimeOffset ProximaTentativaEm { get; private set; }

    /// <summary>Preenchido quando esgota as tentativas; a mensagem sai da fila e fica para análise.</summary>
    public DateTimeOffset? FalhouEm { get; private set; }

    public string? Erro { get; private set; }

    public void MarcarProcessada(DateTimeOffset agora)
    {
        ProcessadaEm = agora;
        Erro = null;
    }

    /// <summary>Agenda nova tentativa com espera crescente (1, 2, 4, 8 min) ou marca falha definitiva.</summary>
    public void RegistrarFalha(string erro, DateTimeOffset agora)
    {
        Tentativas++;
        Erro = erro.Length > 2000 ? erro[..2000] : erro;
        if (Tentativas >= MaximoTentativas)
            FalhouEm = agora;
        else
            ProximaTentativaEm = agora.AddMinutes(Math.Pow(2, Tentativas - 1));
    }
}
