import { keepPreviousData, queryOptions } from '@tanstack/react-query'
import { api } from '@/shared/api/cliente'
import type { components } from '@/shared/api/interno'
import type { StatusTarefa } from './rotulos'

export type TarefaResumo = components['schemas']['TarefaResumoDto']

/** Abas da lista (o quadro mostra as colunas). */
export const ABAS = {
    abertas: { rotulo: 'Em aberto', status: ['A_FAZER', 'FAZENDO'] },
    concluidas: { rotulo: 'Concluídas', status: ['CONCLUIDA'] },
    canceladas: { rotulo: 'Canceladas', status: ['CANCELADA'] },
    todas: { rotulo: 'Todas', status: [] },
} satisfies Record<string, { rotulo: string; status: StatusTarefa[] }>

export type Aba = keyof typeof ABAS

/** Filtros no endereço (`/tarefas?modo=quadro&responsavel=eu&...`); valores padrão omitidos. */
export interface FiltrosTarefas {
    modo?: 'quadro'
    aba?: Aba
    busca?: string
    cliente?: string
    /** Id da pessoa, `eu` ou `ninguem`. */
    responsavel?: string
    pagina?: number
}

export const TAMANHO_PAGINA = 50

function parametros(filtros: FiltrosTarefas) {
    return {
        busca: filtros.busca,
        clienteId: filtros.cliente,
        minhas: filtros.responsavel === 'eu' || undefined,
        semResponsavel: filtros.responsavel === 'ninguem' || undefined,
        responsavelId:
            filtros.responsavel && filtros.responsavel !== 'eu' && filtros.responsavel !== 'ninguem'
                ? filtros.responsavel
                : undefined,
    }
}

export function consultaListaTarefas({ modo, ...filtros }: FiltrosTarefas) {
    const status = ABAS[filtros.aba ?? 'abertas'].status
    return queryOptions({
        queryKey: ['tarefas', 'lista', filtros],
        queryFn: async () =>
            (
                await api.GET('/api/v1/tarefas', {
                    params: {
                        query: {
                            ...parametros(filtros),
                            status: status.length ? status.join(',') : undefined,
                            pagina: filtros.pagina ?? 1,
                            tamanhoPagina: TAMANHO_PAGINA,
                        },
                    },
                })
            ).data!,
        placeholderData: keepPreviousData,
    })
}
