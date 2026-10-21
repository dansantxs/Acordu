import { keepPreviousData, queryOptions } from '@tanstack/react-query'
import { api, seVersao } from '@/shared/api/cliente'
import type { components } from '@/shared/api/interno'

export type Apontamento = components['schemas']['ApontamentoDto']
export type NovoApontamento = components['schemas']['NovoApontamentoRequest']
export type EdicaoApontamento = components['schemas']['EdicaoApontamentoRequest']

export interface FiltrosApontamentos {
    usuarioId?: string
    tarefaId?: string
    clienteId?: string
    contratoId?: string
    de?: string
    ate?: string
}

/** Apontamentos (contrato §9), dos mais recentes; a semana ou a tarefa cabem numa página de 200. */
export function consultaApontamentos(filtros: FiltrosApontamentos) {
    return queryOptions({
        queryKey: ['apontamentos', filtros],
        queryFn: async () =>
            (await api.GET('/api/v1/apontamentos', { params: { query: { ...filtros, tamanhoPagina: 200 } } })).data!,
        placeholderData: keepPreviousData,
    })
}

export async function criarApontamento(dados: NovoApontamento): Promise<Apontamento> {
    return (await api.POST('/api/v1/apontamentos', { body: dados })).data!
}

export async function editarApontamento(a: Apontamento, dados: EdicaoApontamento): Promise<Apontamento> {
    return (
        await api.PATCH('/api/v1/apontamentos/{id}', {
            params: { path: { id: a.id } },
            body: dados,
            ...seVersao(a.versao),
        })
    ).data!
}

export async function excluirApontamento(id: string): Promise<void> {
    await api.DELETE('/api/v1/apontamentos/{id}', { params: { path: { id } } })
}
