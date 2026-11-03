namespace Acordu.Application.Abstractions;

/// <summary>
/// Endereço público do front (SPA interna e portal), usado para montar os links dos e-mails. O token vai no caminho e a
/// SPA o tira da URL na primeira carga (segurança §9, Referrer-Policy).
/// </summary>
public sealed class EnderecosPublicos
{
    private readonly Uri _base;
    private readonly Uri _baseApi;

    /// <param name="baseUrl">Endereço do SPA (<c>App__UrlPublica</c>).</param>
    /// <param name="baseApi">
    /// Endereço da API (<c>App__UrlApi</c>), quando ela está em outro host que o SPA (ADR 0010: <c>api.acordu.app.br</c>);
    /// sem ele, a API fica no mesmo endereço do SPA.
    /// </param>
    public EnderecosPublicos(Uri baseUrl, Uri? baseApi = null)
    {
        _base = ComBarra(baseUrl, nameof(baseUrl));
        _baseApi = baseApi is null ? _base : ComBarra(baseApi, nameof(baseApi));
    }

    private static Uri ComBarra(Uri endereco, string parametro)
    {
        ArgumentNullException.ThrowIfNull(endereco, parametro);
        if (!endereco.IsAbsoluteUri)
            throw new ArgumentException("O endereço público precisa ser absoluto.", parametro);
        return endereco.AbsoluteUri.EndsWith('/') ? endereco : new Uri(endereco.AbsoluteUri + "/");
    }

    /// <summary>Solicitação no app da equipe.</summary>
    public Uri SolicitacaoInterna(Guid solicitacaoId) => new(_base, $"solicitacoes/{solicitacaoId}");

    /// <summary>Contrato no app da equipe.</summary>
    public Uri ContratoInterno(Guid contratoId) => new(_base, $"contratos/{contratoId}");

    /// <summary>Recorrência no app da equipe.</summary>
    public Uri RecorrenciaInterna(Guid recorrenciaId) => new(_base, $"recorrencias/{recorrenciaId}");

    /// <summary>Feed ICS da agenda assinável: o calendário busca direto na API, então usa o endereço dela.</summary>
    public Uri AgendaIcs(string token) => new(_baseApi, $"api/v1/agenda/{token}.ics");

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
