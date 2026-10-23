import { keepPreviousData, queryOptions } from '@tanstack/react-query'
import { api } from '@/shared/api/cliente'
import type { components } from '@/shared/api/interno'
import { somarDias } from '@/shared/format/datas'

export type Agenda = components['schemas']['AgendaDto']
export type ItemAgenda = components['schemas']['ItemAgendaDto']
export type CapacidadeSemana = components['schemas']['CapacidadeSemanaDto']

/**
 * Filtros no endereço: `?inicio=` (segunda da janela; vazio = esta semana), `?faixas=cliente` (padrão: por pessoa) e
 * `?vista=plano` (padrão: linha do tempo). `vista`, e não `modo`, que já é das tarefas com outro valor (convenções §4).
 */
export interface FiltrosAgenda {
    inicio?: string
    faixas?: 'cliente'
    vista?: 'plano'
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

/**
 * Planejar uma tarefa (F6d): o `PATCH` da tarefa recebe o formulário inteiro, então lê o detalhe e troca só o dia
 * planejado e o responsável. Devolve o que havia antes, para o desfazer.
 */
export async function planejarTarefa(
    id: string,
    destino: { planejadaPara: string | null; responsavelId: string | null },
): Promise<{ planejadaPara: string | null; responsavelId: string | null }> {
    const atual = (await api.GET('/api/v1/tarefas/{id}', { params: { path: { id } } })).data!
    const antes = { planejadaPara: atual.planejadaPara, responsavelId: atual.responsavel?.id ?? null }
    await api.PATCH('/api/v1/tarefas/{id}', {
        params: { path: { id } },
        body: {
            titulo: atual.titulo,
            descricao: atual.descricao,
            prazo: atual.prazo,
            estimativaMinutos: atual.estimativaMinutos,
            visivelCliente: atual.visivelCliente,
            ...destino,
        },
        headers: { 'If-Match': `"${atual.versao}"` },
    })
    return antes
}
