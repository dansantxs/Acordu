/**
 * Formatador único de tempo (convenções §4): minutos inteiros → "1h30", "45min", "2h", "0min". Negativos (saldo
 * estourado) ganham o sinal na frente.
 */
export function formatarDuracao(minutos: number): string {
    if (!Number.isInteger(minutos)) throw new RangeError('A duração é sempre em minutos inteiros.')
    const sinal = minutos < 0 ? '−' : ''
    const total = Math.abs(minutos)
    const horas = Math.floor(total / 60)
    const resto = total % 60
    if (horas === 0) return `${sinal}${resto}min`
    if (resto === 0) return `${sinal}${horas}h`
    return `${sinal}${horas}h${String(resto).padStart(2, '0')}`
}

/**
 * Lê uma duração digitada (plano do F3: um campo só): "1h30", "1h", "1:30", "90" (minutos), "45min", "1,5h". Devolve
 * minutos inteiros, ou `null` se não entender ou der zero.
 */
export function lerDuracao(texto: string): number | null {
    const t = texto.trim().toLowerCase().replace(/\s+/g, '')
    if (!t) return null
    let minutos: number | null = null
    let m: RegExpExecArray | null
    if ((m = /^(\d+)h(\d{1,2})?(?:min|m)?$/.exec(t))) minutos = Number(m[1]) * 60 + Number(m[2] ?? 0)
    else if ((m = /^(\d+)[.,](\d+)h$/.exec(t))) minutos = Math.round(Number(`${m[1]}.${m[2]}`) * 60)
    else if ((m = /^(\d+):(\d{2})$/.exec(t))) minutos = Number(m[1]) * 60 + Number(m[2])
    else if ((m = /^(\d+)(?:min|m)?$/.exec(t))) minutos = Number(m[1])
    return minutos && minutos > 0 ? minutos : null
}
