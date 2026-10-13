namespace Acordu.Application.Abstractions;

/// <summary>
/// Marcadores de "aviso já enviado" no tenant do contexto, para avisos que um job detecta repetidamente (SLA, consumo):
/// um por (referência, chave). Gravados na hora, fora da unidade de trabalho.
/// </summary>
public interface IMarcadoresDeAviso
{
    /// <summary>Marca; falso se já estava marcado (o aviso já saiu).</summary>
    Task<bool> MarcarAsync(Guid referencia, string chave, CancellationToken cancellationToken = default);

    /// <summary>Desfaz a marca (envio falhou, ou o limiar foi rearmado).</summary>
    Task DesmarcarAsync(Guid referencia, string chave, CancellationToken cancellationToken = default);
}
