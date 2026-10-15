using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Acordu.Infrastructure.TempoReal;

/// <summary>
/// Sinais de uma gravação, lidos do change tracker antes do <c>SaveChanges</c>: raízes incluídas, alteradas ou excluídas
/// que alguma tela mostra. Interação e comentário sinalizam o item onde aparecem; apontamento, a tarefa.
/// </summary>
internal static class ColetorSinais
{
    public static List<(Guid TenantId, SinalMudanca Sinal)> Coletar(ChangeTracker rastreador)
    {
        var sinais = new List<(Guid, SinalMudanca)>();
        foreach (var entrada in rastreador.Entries<EntidadeBase>())
        {
            if (entrada.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;
            if (Sinal(entrada.Entity) is { } sinal)
                sinais.Add((entrada.Entity.TenantId, sinal));
        }

        return [.. sinais.Distinct()];
    }

    private static SinalMudanca? Sinal(EntidadeBase entidade) => entidade switch
    {
        Solicitacao s => new("solicitacao", s.Id),
        Interacao i => new("solicitacao", i.SolicitacaoId),
        Tarefa t => new("tarefa", t.Id),
        ComentarioTarefa c => new("tarefa", c.TarefaId),
        Apontamento a => new("tarefa", a.TarefaId),
        Projeto p => new("projeto", p.Id),
        RecorrenciaTarefa r => new("recorrencia", r.Id),
        Contrato c => new("contrato", c.Id),
        Cliente c => new("cliente", c.Id),
        _ => null,
    };
}
