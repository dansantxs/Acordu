using Acordu.Domain.Common;
using Acordu.Domain.Enums;
using Acordu.Domain.Events;
using Acordu.Domain.Exceptions;
using Acordu.Domain.Services;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.Entities;

/// <summary>
/// Comentário da equipe numa tarefa, com anexos (modelo de domínio §4.6.1). Só interno: nunca aparece no portal, nem
/// em tarefa visível ao cliente. Agregado próprio porque cresce sem limite; imutável como a <see cref="Interacao"/>, e só
/// o autor exclui (regra na Application, que conhece quem age).
/// </summary>
public sealed class ComentarioTarefa : EntidadeBase
{
    private readonly List<Anexo> _anexos = [];

    private ComentarioTarefa(Guid id, Guid tenantId)
        : base(id, tenantId)
    {
    }

    /// <summary>Usado só pela materialização do EF Core.</summary>
    private ComentarioTarefa()
    {
    }

    public Guid TarefaId { get; private set; }

    public Guid AutorId { get; private set; }

    public string? Texto { get; private set; }

    public DateTimeOffset RegistradoEm { get; private set; }

    public IReadOnlyList<Anexo> Anexos => _anexos;

    /// <summary>
    /// Exige texto ou ao menos um anexo; anexos validados contra a <paramref name="politica"/>. Menções no texto emitem
    /// <see cref="PessoasMencionadas"/>.
    /// </summary>
    public static ComentarioTarefa Registrar(
        Guid id,
        Guid tenantId,
        Guid tarefaId,
        Guid autorId,
        string? texto,
        IReadOnlyList<NovoAnexo> anexos,
        PoliticaAnexos politica,
        DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(anexos);
        ArgumentNullException.ThrowIfNull(politica);
        var textoValido = Common.Texto.Opcional(texto, LimitesTexto.TextoInteracao, nameof(texto));
        if (textoValido is null && anexos.Count == 0)
            throw new ArgumentException("O comentário precisa de texto ou de ao menos um anexo.", nameof(texto));
        if (anexos.Count > politica.MaximoPorInteracao)
            throw new AnexoInvalidoException($"No máximo {politica.MaximoPorInteracao} anexos por comentário.");

        var comentario = new ComentarioTarefa(id, tenantId)
        {
            TarefaId = Ids.Obrigatorio(tarefaId, nameof(tarefaId)),
            AutorId = Ids.Obrigatorio(autorId, nameof(autorId)),
            Texto = textoValido,
            RegistradoEm = agora.ToUniversalTime(),
        };
        foreach (var anexo in anexos)
            comentario._anexos.Add(Anexo.Criar(anexo, politica));

        comentario.Registrar(new ComentarioTarefaRegistrado(id, tenantId, comentario.TarefaId, comentario.AutorId));
        var mencionados = Mencoes.Extrair(textoValido, comentario.AutorId);
        if (mencionados.Count > 0)
        {
            comentario.Registrar(new PessoasMencionadas(
                tenantId, AlvoColaboracao.Tarefa, comentario.TarefaId, id, comentario.AutorId, mencionados));
        }

        return comentario;
    }
}
