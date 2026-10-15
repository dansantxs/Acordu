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
