import { queryOptions } from '@tanstack/react-query'
import { api } from '../api/cliente'
import type { components } from '../api/interno'

export type Cronometro = components['schemas']['CronometroDto']
export type Apontamento = components['schemas']['ApontamentoDto']

/** Um por pessoa (RF-APT-02). Sem cronômetro, a API responde 204 e a consulta fica `null`. */
export const consultaCronometro = queryOptions({
    queryKey: ['cronometro'],
    queryFn: async (): Promise<Cronometro | null> => {
        const { data, response } = await api.GET('/api/v1/cronometro')
        return response.status === 204 ? null : (data ?? null)
    },
})

/** Minutos desde o início, contados no navegador (a API arredonda igual ao parar, com mínimo de 1). */
export function decorridoMinutos(c: Cronometro, agora = Date.now()): number {
    return Math.max(0, Math.round((agora - new Date(c.iniciadoEm).getTime()) / 60_000))
}

export async function iniciarCronometro(tarefaId: string): Promise<void> {
    await api.POST('/api/v1/cronometro/inicio', { body: { tarefaId } })
}

export interface Parada {
    descricao: string | null
    descricaoExterna: string | null
    faturavel: boolean
    visivelCliente: boolean
}

export async function pararCronometro(dados: Parada): Promise<Apontamento> {
    // O corpo é opcional e lido à mão pela API, por isso não está no OpenAPI (contrato §9).
    return (await api.POST('/api/v1/cronometro/parada', { body: dados as never })).data!
}

export async function descartarCronometro(): Promise<void> {
    await api.DELETE('/api/v1/cronometro')
}
