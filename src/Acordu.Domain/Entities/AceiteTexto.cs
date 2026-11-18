using Acordu.Domain.Enums;

namespace Acordu.Domain.Entities;

/// <summary>
/// Registro de que uma conta aceitou (ou marcou "ciente" de) uma versão de um texto legal (RF-AUT-11, design do F14,
/// D10 e D16): global, fora dos tenants, apagado com a conta. Uma linha por conta, texto e versão; a versão vigente sem
/// linha conta como pendente (F14-83, F14-119).
/// </summary>
public sealed class AceiteTexto
{
    /// <summary>Tamanho máximo da versão (as constantes são curtas: "1", "2", "2027-01").</summary>
    public const int TamanhoVersao = 20;

    // Só o EF materializa.
    private AceiteTexto()
    {
        Versao = null!;
    }

    private AceiteTexto(Guid usuarioId, TipoTextoLegal texto, string versao, FormaAceiteTexto forma, DateTimeOffset registradoEm)
    {
        UsuarioId = usuarioId;
        Texto = texto;
        Versao = versao;
        Forma = forma;
        RegistradoEm = registradoEm;
    }

    /// <summary>A conta (global, não um vínculo de empresa).</summary>
    public Guid UsuarioId { get; private set; }

    public TipoTextoLegal Texto { get; private set; }

    public string Versao { get; private set; }

    public FormaAceiteTexto Forma { get; private set; }

    public DateTimeOffset RegistradoEm { get; private set; }

    /// <summary>Registra a versão <paramref name="versao"/> do texto para a conta, na data dada (em UTC).</summary>
    public static AceiteTexto Registrar(
        Guid usuarioId, TipoTextoLegal texto, string versao, FormaAceiteTexto forma, DateTimeOffset agora)
    {
        if (usuarioId == Guid.Empty)
            throw new ArgumentException("A conta é obrigatória.", nameof(usuarioId));
        if (!Enum.IsDefined(texto))
            throw new ArgumentOutOfRangeException(nameof(texto), texto, "Texto legal desconhecido.");
        if (!Enum.IsDefined(forma))
            throw new ArgumentOutOfRangeException(nameof(forma), forma, "Forma de aceite desconhecida.");
        var valor = versao?.Trim();
        if (string.IsNullOrEmpty(valor) || valor.Length > TamanhoVersao)
            throw new ArgumentException($"A versão precisa ter de 1 a {TamanhoVersao} caracteres.", nameof(versao));
        return new AceiteTexto(usuarioId, texto, valor, forma, agora.ToUniversalTime());
    }
}
