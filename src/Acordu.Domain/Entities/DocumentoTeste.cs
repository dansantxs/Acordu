using Acordu.Domain.Exceptions;

namespace Acordu.Domain.Entities;

/// <summary>
/// Registro de que um CPF ou CNPJ já teve teste (RF-AUT-09, decisão 8 do G2 do F14, design D8): global, fora dos tenants,
/// sem referência à empresa nem à pessoa. Guarda só o HMAC-SHA256 do documento normalizado (a chave fica fora do banco),
/// para a regra "um teste por documento" valer mesmo depois do expurgo da empresa sem guardar o documento em claro. A
/// liberação para novo teste (F14-71, dada pela área da Nebrys) vale para um cadastro só (F14-59).
/// </summary>
public sealed class DocumentoTeste
{
    /// <summary>HMAC-SHA256 em hexadecimal minúsculo.</summary>
    public const int TamanhoHash = 64;

    // Só o EF materializa.
    private DocumentoTeste()
    {
        Hash = null!;
    }

    private DocumentoTeste(string hash, DateTimeOffset primeiroTesteEm)
    {
        Hash = hash;
        PrimeiroTesteEm = primeiroTesteEm;
    }

    /// <summary>HMAC do documento normalizado; é a chave do registro (único).</summary>
    public string Hash { get; private set; }

    /// <summary>Quando o documento teve o primeiro teste (o cadastro que criou o registro).</summary>
    public DateTimeOffset PrimeiroTesteEm { get; private set; }

    /// <summary>Quando a Nebrys liberou um novo teste (F14-71, F14-108); nulo sem liberação.</summary>
    public DateTimeOffset? LiberadoEm { get; private set; }

    /// <summary>Quando um cadastro usou a liberação; nulo enquanto ela está disponível ou não existe.</summary>
    public DateTimeOffset? LiberacaoUsadaEm { get; private set; }

    /// <summary>Há uma liberação ainda não usada: um cadastro novo com o documento passa (F14-59).</summary>
    public bool LiberacaoDisponivel => LiberadoEm is not null && LiberacaoUsadaEm is null;

    /// <summary>Primeiro teste do documento (cadastro que passou, F14-57).</summary>
    public static DocumentoTeste Registrar(string hash, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(hash);
        if (hash.Length != TamanhoHash || !hash.All(c => char.IsAsciiDigit(c) || c is >= 'a' and <= 'f'))
            throw new ArgumentException("O hash do documento precisa ter 64 dígitos hexadecimais minúsculos.", nameof(hash));
        return new DocumentoTeste(hash, agora.ToUniversalTime());
    }

    /// <summary>
    /// A Nebrys libera um novo teste para o documento (F14-71, F14-108): o próximo cadastro com ele passa (F14-59). Uma
    /// liberação já usada não volta sozinha (decisão do Daniel de 18/11, F14-104); só uma liberação nova a substitui. Com
    /// uma liberação ainda não usada, não há o que liberar → <see cref="TransicaoInvalidaException"/>.
    /// </summary>
    public void Liberar(DateTimeOffset agora)
    {
        if (LiberacaoDisponivel)
            throw new TransicaoInvalidaException("Este documento já tem uma liberação para novo teste ainda não usada.");
        LiberadoEm = agora.ToUniversalTime();
        LiberacaoUsadaEm = null;
    }

    /// <summary>
    /// Um cadastro novo usa a liberação (F14-59): ela fica usada e não vale para outro. Sem liberação disponível →
    /// <see cref="TransicaoInvalidaException"/>.
    /// </summary>
    public void UsarLiberacao(DateTimeOffset agora)
    {
        if (!LiberacaoDisponivel)
            throw new TransicaoInvalidaException("Este documento não tem liberação para novo teste.");
        LiberacaoUsadaEm = agora.ToUniversalTime();
    }
}
