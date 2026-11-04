using Acordu.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Acordu.Infrastructure.Email;

/// <summary>Log dos avisos descartados por empresa não liberada (RF-AUT-06, F9b): categoria e empresa, sem endereço.</summary>
public sealed partial class EnviosBloqueados : IEnviosBloqueados
{
    private readonly ILogger<EnviosBloqueados> _logger;
    private readonly ITenantContext _contexto;

    public EnviosBloqueados(ILogger<EnviosBloqueados> logger, ITenantContext contexto)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(contexto);
        _logger = logger;
        _contexto = contexto;
    }

    public void Registrar(string categoria) => Descartado(_logger, categoria, _contexto.TenantId);

    [LoggerMessage(EventId = 110, Level = LogLevel.Information,
        Message = "Aviso '{Categoria}' descartado: a empresa {TenantId} ainda não confirmou o e-mail do criador.")]
    private static partial void Descartado(ILogger logger, string categoria, Guid? tenantId);
}
