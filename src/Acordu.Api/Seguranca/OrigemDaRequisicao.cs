using Acordu.Application.Abstractions;

namespace Acordu.Api.Seguranca;

/// <summary>IP do cliente da requisição corrente (com <c>UseForwardedHeaders</c>, o que o Caddy viu).</summary>
internal sealed class OrigemDaRequisicao(IHttpContextAccessor http) : IOrigemRequisicao
{
    public string? Ip => http.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
