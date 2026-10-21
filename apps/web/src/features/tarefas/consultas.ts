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

/** Dias de concluídas que o quadro mostra (decidido em 21/10). */
export const DIAS_CONCLUIDAS_NO_QUADRO = 14

/** Quadro: abertas e concluídas recentes numa chamada (`concluidaDesde` só recorta as concluídas). */
export function consultaQuadro({ modo, aba, pagina, ...filtros }: FiltrosTarefas, agora = Date.now()) {
    const desde = new Date(agora - DIAS_CONCLUIDAS_NO_QUADRO * 86_400_000)
    // Arredonda para a hora: a chave da consulta não muda a cada renderização.
    desde.setUTCMinutes(0, 0, 0)
    return queryOptions({
        queryKey: ['tarefas', 'quadro', filtros],
        queryFn: async () =>
            (
                await api.GET('/api/v1/tarefas', {
                    params: {
                        query: {
                            ...parametros(filtros),
                            status: 'A_FAZER,FAZENDO,CONCLUIDA',
                            concluidaDesde: desde.toISOString(),
                            tamanhoPagina: 200,
                        },
                    },
                })
            ).data!,
    })
}

export type Coluna = Exclude<StatusTarefa, 'CANCELADA'>

/**
 * Chamadas para levar a tarefa de uma coluna a outra pela máquina de estados (modelo §4.6): de "A fazer" para
 * "Concluída" passa por "Fazendo"; de "Concluída" para "A fazer", reabre e retorna.
 */
export function caminho(de: Coluna, para: Coluna): ('inicio' | 'conclusao' | 'reabertura' | 'retorno')[] {
    const passos: Record<Coluna, Partial<Record<Coluna, ('inicio' | 'conclusao' | 'reabertura' | 'retorno')[]>>> = {
        A_FAZER: { FAZENDO: ['inicio'], CONCLUIDA: ['inicio', 'conclusao'] },
        FAZENDO: { A_FAZER: ['retorno'], CONCLUIDA: ['conclusao'] },
        CONCLUIDA: { FAZENDO: ['reabertura'], A_FAZER: ['reabertura', 'retorno'] },
    }
    return passos[de][para] ?? []
}

export type Tarefa = components['schemas']['TarefaDto']

export async function moverTarefa(id: string, de: Coluna, para: Coluna): Promise<Tarefa | null> {
    let ultima: Tarefa | null = null
    const opcoes = { params: { path: { id } } }
    for (const passo of caminho(de, para)) {
        ultima =
            passo === 'inicio'
                ? (await api.POST('/api/v1/tarefas/{id}/inicio', opcoes)).data!
                : passo === 'conclusao'
                  ? (await api.POST('/api/v1/tarefas/{id}/conclusao', opcoes)).data!
                  : passo === 'reabertura'
                    ? (await api.POST('/api/v1/tarefas/{id}/reabertura', opcoes)).data!
                    : (await api.POST('/api/v1/tarefas/{id}/retorno', opcoes)).data!
    }
    return ultima
}
