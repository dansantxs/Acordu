using System.Text.Json;
using Acordu.Application.Notificacoes;

namespace Acordu.Infrastructure.Persistence.Notificacoes;

/// <summary>
/// Notificação do sino (RF-NOT-03), fora do domínio como a auditoria. Incluída por SQL (<c>ON CONFLICT DO NOTHING</c>
/// em tenant + usuário + chave) e lida/marcada pelo EF; apagada pelo expurgo depois de 90 dias.
/// </summary>
public sealed class Notificacao
{
    public const int TamanhoChave = 200;
    public const int TamanhoTitulo = 300;
    public const int TamanhoLink = 1000;

    private Notificacao()
    {
        Categoria = null!;
        Chave = null!;
        Titulo = null!;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid UsuarioId { get; private set; }

    /// <summary>Categoria no formato do JSON (<c>NOVA_SOLICITACAO</c>).</summary>
    public string Categoria { get; private set; }

    public string Chave { get; private set; }

    public string Titulo { get; private set; }

    public string? Link { get; private set; }

    public DateTimeOffset CriadaEm { get; private set; }

    public DateTimeOffset? LidaEm { get; private set; }

    internal static string Texto(CategoriaAviso categoria) => JsonNamingPolicy.SnakeCaseUpper.ConvertName(categoria.ToString());

    internal static CategoriaAviso DeTexto(string texto) =>
        Enum.GetValues<CategoriaAviso>().First(c => Texto(c) == texto);
}

/// <summary>Categorias de e-mail desligadas por pessoa e tenant (RF-NOT-04). Ausente = tudo ligado.</summary>
public sealed class PreferenciaNotificacao
{
    public PreferenciaNotificacao(Guid tenantId, Guid usuarioId)
    {
        TenantId = tenantId;
        UsuarioId = usuarioId;
    }

    public Guid TenantId { get; private set; }

    public Guid UsuarioId { get; private set; }

    public List<string> EmailDesligado { get; private set; } = [];

    public void Definir(IEnumerable<string> categorias) => EmailDesligado = [.. categorias.Order(StringComparer.Ordinal)];
}
