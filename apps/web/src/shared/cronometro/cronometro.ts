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

/** `iniciadoEm` (ISO): começou antes (F12-01), até 12 h atrás; sem, começa agora. */
export async function iniciarCronometro(tarefaId: string, iniciadoEm?: string): Promise<void> {
    await api.POST('/api/v1/cronometro/inicio', { body: { tarefaId, iniciadoEm: iniciadoEm ?? null } })
}

/**
 * "Começou às HH:MM" no relógio do aparelho: hoje ou, se esse horário ainda não chegou, ontem (a API limita a 12 h
 * atrás). Devolve `null` para um horário mal escrito.
 */
export function inicioPelaHora(hora: string, agora = new Date()): Date | null {
    const casamento = /^(\d{1,2}):(\d{2})$/.exec(hora.trim())
    if (!casamento) return null
    const [h, m] = [Number(casamento[1]), Number(casamento[2])]
    if (h > 23 || m > 59) return null
    const inicio = new Date(agora)
    inicio.setHours(h, m, 0, 0)
    if (inicio.getTime() > agora.getTime()) inicio.setDate(inicio.getDate() - 1)
    return inicio
}

export interface Parada {
    descricao: string | null
    descricaoExterna: string | null
    faturavel: boolean
    visivelCliente: boolean
    /** Só quando a pessoa mudou a duração (F12-05); sem, a API aponta o decorrido. */
    duracaoMinutos?: number
}

export async function pararCronometro(dados: Parada): Promise<Apontamento> {
    // O corpo é opcional e lido à mão pela API, por isso não está no OpenAPI (contrato §9).
    return (await api.POST('/api/v1/cronometro/parada', { body: dados as never })).data!
}

export async function descartarCronometro(): Promise<void> {
    await api.DELETE('/api/v1/cronometro')
}
