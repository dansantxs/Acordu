namespace Acordu.Infrastructure.Armazenamento;

/// <summary>
/// Object storage pela API compatível com S3 (OCI: <c>https://{namespace}.compat.objectstorage.{regiao}.oraclecloud.com</c>,
/// Customer Secret Key). Sem <see cref="Endpoint"/>, os anexos ficam indisponíveis (dev sem storage).
/// </summary>
public sealed record OpcoesArmazenamento(string? Endpoint, string Regiao, string Bucket, string? ChaveAcesso, string? ChaveSecreta)
{
    public const string BucketPadrao = "acordu-anexos";
    public const string RegiaoPadrao = "sa-saopaulo-1";

    public bool Configurado => !string.IsNullOrWhiteSpace(Endpoint);
}
