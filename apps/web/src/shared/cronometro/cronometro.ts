import { queryOptions } from '@tanstack/react-query'
import { api } from '../api/cliente'
import type { components } from '../api/interno'
import { hojeNoFuso, somarDias } from '../format/datas'

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

/** Hora de parede (ano, mês, dia, hora, minuto) de um instante num fuso, em milissegundos "como se fosse UTC". */
function paredeEmMs(instante: number, fuso: string): number {
    const partes = Object.fromEntries(
        new Intl.DateTimeFormat('en-US', {
            timeZone: fuso,
            hourCycle: 'h23',
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit',
        })
            .formatToParts(instante)
            .map((p) => [p.type, Number(p.value)]),
    ) as Record<string, number>
    return Date.UTC(partes.year!, partes.month! - 1, partes.day!, partes.hour!, partes.minute!)
}

/** Instante de `data` (`YYYY-MM-DD`) às `h:m` no fuso (convenções: instantes no fuso do tenant). */
function instanteNoFuso(data: string, h: number, m: number, fuso: string): number {
    const [ano, mes, dia] = data.split('-').map(Number)
    const parede = Date.UTC(ano!, mes! - 1, dia!, h, m)
    // Duas voltas acertam também a véspera de mudança de horário de verão.
    let instante = parede - (paredeEmMs(parede, fuso) - parede)
    instante = parede - (paredeEmMs(instante, fuso) - instante)
    return instante
}

/**
 * "Começou às HH:MM" no fuso da empresa: hoje ou, se esse horário ainda não chegou, ontem (a API limita a 12 h
 * atrás). Devolve `null` para um horário mal escrito.
 */
export function inicioPelaHora(hora: string, fuso: string, agora = new Date()): Date | null {
    const casamento = /^(\d{1,2}):(\d{2})$/.exec(hora.trim())
    if (!casamento) return null
    const [h, m] = [Number(casamento[1]), Number(casamento[2])]
    if (h > 23 || m > 59) return null
    const hoje = hojeNoFuso(fuso, agora)
    const inicio = instanteNoFuso(hoje, h, m, fuso)
    return new Date(inicio > agora.getTime() ? instanteNoFuso(somarDias(hoje, -1), h, m, fuso) : inicio)
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
