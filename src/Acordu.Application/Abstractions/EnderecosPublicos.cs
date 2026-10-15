namespace Acordu.Application.Abstractions;

/// <summary>
/// Endereço público do front (SPA interna e portal), usado para montar os links dos e-mails. O token vai no caminho e a
/// SPA o tira da URL na primeira carga (segurança §9, Referrer-Policy).
/// </summary>
public sealed class EnderecosPublicos
{
    private readonly Uri _base;

    public EnderecosPublicos(Uri baseUrl)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        if (!baseUrl.IsAbsoluteUri)
            throw new ArgumentException("O endereço público precisa ser absoluto.", nameof(baseUrl));
        _base = baseUrl.AbsoluteUri.EndsWith('/') ? baseUrl : new Uri(baseUrl.AbsoluteUri + "/");
    }

    /// <summary>Solicitação no app da equipe.</summary>
    public Uri SolicitacaoInterna(Guid solicitacaoId) => new(_base, $"solicitacoes/{solicitacaoId}");

    /// <summary>Contrato no app da equipe.</summary>
    public Uri ContratoInterno(Guid contratoId) => new(_base, $"contratos/{contratoId}");

    /// <summary>Recorrência no app da equipe.</summary>
    public Uri RecorrenciaInterna(Guid recorrenciaId) => new(_base, $"recorrencias/{recorrenciaId}");

    /// <summary>Feed ICS da agenda assinável, servido pela API no mesmo endereço (Caddy encaminha <c>/api</c>).</summary>
    public Uri AgendaIcs(string token) => new(_base, $"api/v1/agenda/{token}.ics");

    /// <summary>Tarefa no app da equipe.</summary>
    public Uri TarefaInterna(Guid tarefaId) => new(_base, $"tarefas/{tarefaId}");

    /// <summary>Solicitação no portal, para o contato com conta.</summary>
    public Uri SolicitacaoNoPortal(Guid solicitacaoId) => new(_base, $"portal/solicitacoes/{solicitacaoId}");

    /// <summary>Link de acesso a uma solicitação, sem senha (RF-POR-08).</summary>
    public Uri LinkPortal(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        return new Uri(_base, $"portal/l/{token}");
    }

    /// <summary>Confirmação de e-mail da conta.</summary>
    public Uri ConfirmacaoEmail(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        return new Uri(_base, $"conta/confirmacao/{token}");
    }

    /// <summary>Redefinição de senha.</summary>
    public Uri RedefinicaoSenha(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        return new Uri(_base, $"conta/senha/{token}");
    }

    /// <summary>Página de aceite do convite para a equipe.</summary>
    public Uri ConviteEquipe(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        return new Uri(_base, $"convite/{token}");
    }

    /// <summary>Página de aceite do convite do portal.</summary>
    public Uri ConvitePortal(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        return new Uri(_base, $"portal/convite/{token}");
    }
}
