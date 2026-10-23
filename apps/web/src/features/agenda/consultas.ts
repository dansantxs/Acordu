import { keepPreviousData, queryOptions } from '@tanstack/react-query'
import { api } from '@/shared/api/cliente'
import type { components } from '@/shared/api/interno'
import { somarDias } from '@/shared/format/datas'

export type Agenda = components['schemas']['AgendaDto']
export type ItemAgenda = components['schemas']['ItemAgendaDto']
export type CapacidadeSemana = components['schemas']['CapacidadeSemanaDto']

/** Filtros no endereço: `?inicio=` (segunda da janela; vazio = esta semana) e `?faixas=cliente` (padrão: por pessoa). */
export interface FiltrosAgenda {
    inicio?: string
    faixas?: 'cliente'
}

/** Janela da linha do tempo (decisão 3 do F6): duas semanas de segunda a domingo, mostradas em 10 dias úteis. */
export function consultaAgenda(inicio: string) {
    return queryOptions({
        queryKey: ['agenda', inicio],
        queryFn: async () =>
            (await api.GET('/api/v1/agenda', { params: { query: { de: inicio, ate: somarDias(inicio, 13) } } })).data!,
        placeholderData: keepPreviousData,
    })
}

export function consultaCapacidade(semana: string) {
    return queryOptions({
        queryKey: ['tarefas', 'capacidade', semana],
        queryFn: async () => (await api.GET('/api/v1/capacidade', { params: { query: { semana } } })).data!,
    })
}
