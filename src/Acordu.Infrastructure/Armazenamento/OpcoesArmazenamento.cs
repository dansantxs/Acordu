namespace Acordu.Infrastructure.Armazenamento;

/// <summary>
/// Object storage pela API compatível com S3 (R2: <c>https://{conta}.r2.cloudflarestorage.com</c>, região <c>auto</c>, token
/// restrito ao bucket; ADR 0010). Sem <see cref="Endpoint"/>, os anexos ficam indisponíveis (dev sem storage).
/// </summary>
public sealed record OpcoesArmazenamento(string? Endpoint, string Regiao, string Bucket, string? ChaveAcesso, string? ChaveSecreta)
{
    public const string BucketPadrao = "acordu-anexos";
    public const string RegiaoPadrao = "auto";

    public bool Configurado => !string.IsNullOrWhiteSpace(Endpoint);
}
