/**
 * Do sinal de mudança (ADR 0009) à chave do TanStack Query. Convenção (§4): toda consulta começa pelo recurso no
 * plural (`['solicitacoes', …]`), e as visões que juntam vários recursos têm chave própria. Um sinal invalida o
 * recurso e todas as visões; a API reaplica permissões e filtros ao buscar de novo.
 */
export type TipoSinal = 'solicitacao' | 'tarefa' | 'projeto' | 'recorrencia' | 'contrato' | 'cliente'

export const RECURSO_DO_SINAL: Record<TipoSinal, string> = {
    solicitacao: 'solicitacoes',
    tarefa: 'tarefas',
    projeto: 'projetos',
    recorrencia: 'recorrencias',
    contrato: 'contratos',
    cliente: 'clientes',
}

/** Telas que juntam vários recursos: qualquer sinal pode mudá-las. */
export const VISOES = ['agenda', 'capacidade', 'panorama', 'atividades', 'hoje', 'paleta'] as const

export function chavesAfetadas(tipos: Iterable<string>): string[] {
    const chaves = new Set<string>()
    for (const tipo of tipos) {
        const recurso = RECURSO_DO_SINAL[tipo as TipoSinal]
        if (recurso) chaves.add(recurso)
    }
    if (chaves.size > 0) VISOES.forEach((visao) => chaves.add(visao))
    return [...chaves]
}
