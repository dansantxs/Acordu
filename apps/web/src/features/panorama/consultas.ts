import { keepPreviousData, queryOptions } from '@tanstack/react-query'
import { api } from '@/shared/api/cliente'
import { fimDoMes, mesAtual, somarMeses } from '@/shared/format/datas'

/** Período no endereço: `?periodo=anterior` (mês passado) ou `?de=&ate=` (intervalo); nada = este mês. */
export interface FiltrosPanorama {
    periodo?: 'anterior'
    de?: string
    ate?: string
}

export type ModoPeriodo = 'atual' | 'anterior' | 'intervalo'

export function modoDe(f: FiltrosPanorama): ModoPeriodo {
    return f.de || f.ate ? 'intervalo' : f.periodo === 'anterior' ? 'anterior' : 'atual'
}

/** O intervalo que vai para a API (datas no fuso do tenant). */
export function intervalo(f: FiltrosPanorama, fuso: string): { de: string; ate: string } {
    const mes = mesAtual(fuso)
    if (modoDe(f) === 'intervalo') return { de: f.de ?? `${mes}-01`, ate: f.ate ?? fimDoMes(mes) }
    const alvo = f.periodo === 'anterior' ? somarMeses(mes, -1) : mes
    return { de: `${alvo}-01`, ate: fimDoMes(alvo) }
}

export function consultaPanorama(de: string, ate: string) {
    return queryOptions({
        queryKey: ['dashboard', de, ate],
        queryFn: async () => (await api.GET('/api/v1/dashboard', { params: { query: { de, ate } } })).data!,
        placeholderData: keepPreviousData,
    })
}
