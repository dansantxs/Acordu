import { somarDias } from '@/shared/format/datas'

/** Domingo de Páscoa (algoritmo de Meeus/Jones/Butcher, calendário gregoriano). */
export function pascoa(ano: number): string {
    const a = ano % 19
    const b = Math.floor(ano / 100)
    const c = ano % 100
    const d = Math.floor(b / 4)
    const e = b % 4
    const f = Math.floor((b + 8) / 25)
    const g = Math.floor((b - f + 1) / 3)
    const h = (19 * a + b - d - g + 15) % 30
    const i = Math.floor(c / 4)
    const k = c % 4
    const l = (32 + 2 * e + 2 * i - h - k) % 7
    const m = Math.floor((a + 11 * h + 22 * l) / 451)
    const mes = Math.floor((h + l - 7 * m + 114) / 31)
    const dia = ((h + l - 7 * m + 114) % 31) + 1
    return `${ano}-${String(mes).padStart(2, '0')}-${String(dia).padStart(2, '0')}`
}

export interface FeriadoSugerido {
    data: string
    nome: string
}

/**
 * Feriados nacionais do ano (Lei 662/1949 e alterações, mais a Paixão de Cristo e o Dia da Consciência Negra, nacional
 * desde a Lei 14.759/2023), calculados aqui, sem serviço externo (decisão 5 do F5). Carnaval e Corpus Christi são ponto
 * facultativo e ficam de fora: a empresa adiciona se folgar. Estaduais e municipais também.
 */
export function feriadosNacionais(ano: number): FeriadoSugerido[] {
    const d = (mes: number, dia: number) => `${ano}-${String(mes).padStart(2, '0')}-${String(dia).padStart(2, '0')}`
    return [
        { data: d(1, 1), nome: 'Confraternização Universal' },
        { data: somarDias(pascoa(ano), -2), nome: 'Paixão de Cristo' },
        { data: d(4, 21), nome: 'Tiradentes' },
        { data: d(5, 1), nome: 'Dia do Trabalho' },
        { data: d(9, 7), nome: 'Independência do Brasil' },
        { data: d(10, 12), nome: 'Nossa Senhora Aparecida' },
        { data: d(11, 2), nome: 'Finados' },
        { data: d(11, 15), nome: 'Proclamação da República' },
        { data: d(11, 20), nome: 'Dia Nacional de Zumbi e da Consciência Negra' },
        { data: d(12, 25), nome: 'Natal' },
    ]
}
