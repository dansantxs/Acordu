/**
 * Lembrete no expediente (F12d, decisão D4): por pessoa e por navegador, desligado por padrão. Com o app aberto e o
 * expediente da empresa correndo, avisa depois de N minutos sem cronômetro, e não de novo antes de outros N.
 */

export const INTERVALOS_LEMBRETE = [15, 30, 60] as const
export type IntervaloLembrete = (typeof INTERVALOS_LEMBRETE)[number]

const PREFIXO = 'acordu:lembrete-cronometro:'

export function intervaloLembrete(escopo: string): IntervaloLembrete | null {
    try {
        const valor = Number(window.localStorage.getItem(PREFIXO + escopo))
        return (INTERVALOS_LEMBRETE as readonly number[]).includes(valor) ? (valor as IntervaloLembrete) : null
    } catch {
        return null
    }
}

export function definirLembrete(escopo: string, intervalo: IntervaloLembrete | null): void {
    try {
        if (intervalo === null) window.localStorage.removeItem(PREFIXO + escopo)
        else window.localStorage.setItem(PREFIXO + escopo, String(intervalo))
    } catch {
        // Sem armazenamento (navegação privada restrita): o lembrete fica desligado.
    }
}

/**
 * Hora de lembrar? O tempo "sem cronômetro" conta do mais recente entre o início do expediente de hoje, a abertura do
 * app, a última vez que havia cronômetro e o último lembrete (F12-13, F12-15).
 */
export function deveLembrar(estado: {
    agora: number
    intervaloMinutos: number
    /** `null` fora do expediente (F12-14). */
    inicioDoExpediente: number | null
    referencias: (number | null)[]
}): boolean {
    if (estado.inicioDoExpediente === null) return false
    const desde = Math.max(estado.inicioDoExpediente, ...estado.referencias.filter((r): r is number => r !== null))
    return estado.agora - desde >= estado.intervaloMinutos * 60_000
}
