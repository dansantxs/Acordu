using Acordu.Domain.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.Exceptions;
using Catalogo = Acordu.Domain.Acesso.Permissoes;

namespace Acordu.Domain.Entities;

/// <summary>
/// Conjunto de permissões de um tenant (segurança §2–3). Papel CLIENTE só contém permissões do portal; papel INTERNO,
/// só internas. Os quatro padrões (§3.2) são semeados no registro e não mudam: papéis sob medida são criados à parte.
/// </summary>
public sealed class Papel : EntidadeBase
{
    public const string NomeDono = "Dono";
    public const string NomeMembro = "Membro";
    public const string NomeLeitor = "Leitor";
    public const string NomeCliente = "Cliente";

    /// <summary>Internas que o Membro não tem (§3.2).</summary>
    private static readonly HashSet<string> ForaDoMembro = new(StringComparer.Ordinal)
    {
        Catalogo.ContratoGerenciar, Catalogo.ContratoAjustarSaldo, Catalogo.ContratoFecharPeriodo,
        Catalogo.ApontamentoEditarTodos, Catalogo.ConfiguracaoGerenciar, Catalogo.UsuarioConvidar,
        Catalogo.UsuarioRemover, Catalogo.PapelGerenciar, Catalogo.AuditoriaVer,
    };

    // Lista ordenada (e não conjunto) para o EF mapear como text[] e a comparação ser estável.
    private List<string> _permissoes = [];

    private Papel(Guid id, Guid tenantId, string nome, TipoPapel tipo, bool padrao)
        : base(id, tenantId)
    {
        Nome = nome;
        Tipo = tipo;
        Padrao = padrao;
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private Papel()
    {
        Nome = null!;
    }

    public string Nome { get; private set; }

    public TipoPapel Tipo { get; private set; }

    /// <summary>Semeado no registro (§3.2): não é renomeado, alterado nem excluído.</summary>
    public bool Padrao { get; private set; }

    public IReadOnlyCollection<string> Permissoes => _permissoes;

    /// <summary>Papel sob medida do tenant.</summary>
    public static Papel Criar(Guid id, Guid tenantId, string nome, TipoPapel tipo, IEnumerable<string> permissoes)
    {
        if (!Enum.IsDefined(tipo))
            throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de papel inválido.");

        var papel = new Papel(id, tenantId, Texto.Obrigatorio(nome, LimitesTexto.NomePapel, nameof(nome)), tipo, padrao: false);
        papel._permissoes = Validar(tipo, permissoes);
        return papel;
    }

    /// <summary>Dono, Membro, Leitor e Cliente, como na tabela do §3.2.</summary>
    public static IReadOnlyList<Papel> Padroes(Guid tenantId) =>
    [
        Semear(tenantId, NomeDono, TipoPapel.Interno, Catalogo.Internas),
        Semear(tenantId, NomeMembro, TipoPapel.Interno, Catalogo.Internas.Where(p => !ForaDoMembro.Contains(p))),
        Semear(
            tenantId,
            NomeLeitor,
            TipoPapel.Interno,
            Catalogo.Internas.Where(p => p.EndsWith(".ver", StringComparison.Ordinal))),
        Semear(tenantId, NomeCliente, TipoPapel.Cliente, Catalogo.DoPortal),
    ];

    public bool Possui(string permissao) => _permissoes.BinarySearch(permissao, StringComparer.Ordinal) >= 0;

    public void Renomear(string nome)
    {
        GarantirSobMedida();
        Nome = Texto.Obrigatorio(nome, LimitesTexto.NomePapel, nameof(nome));
    }

    public void DefinirPermissoes(IEnumerable<string> permissoes)
    {
        GarantirSobMedida();
        _permissoes = Validar(Tipo, permissoes);
    }

    private static Papel Semear(Guid tenantId, string nome, TipoPapel tipo, IEnumerable<string> permissoes) =>
        new(Guid.CreateVersion7(), tenantId, nome, tipo, padrao: true) { _permissoes = Validar(tipo, permissoes) };

    private static List<string> Validar(TipoPapel tipo, IEnumerable<string> permissoes)
    {
        ArgumentNullException.ThrowIfNull(permissoes);
        var permitidas = Catalogo.Permitidas(tipo);
        var lista = permissoes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (lista.Count == 0)
            throw new ArgumentException("O papel precisa de ao menos uma permissão.", nameof(permissoes));

        var invalida = lista.FirstOrDefault(p => !permitidas.Contains(p));
        if (invalida is not null)
        {
            throw new ArgumentException(
                $"A permissão '{invalida}' não pode compor um papel {(tipo == TipoPapel.Cliente ? "de cliente" : "interno")}.",
                nameof(permissoes));
        }

        return lista;
    }

    private void GarantirSobMedida()
    {
        if (Padrao)
            throw new TransicaoInvalidaException("Papel padrão não pode ser alterado.");
    }
}
