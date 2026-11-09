/**
 * Lembrete no expediente (F12d, decisão D4): por pessoa e por navegador, desligado por padrão. Com o app aberto e o
 * expediente da empresa correndo, avisa depois de N minutos sem cronômetro, e não de novo antes de outros N.
 */

export const INTERVALOS_LEMBRETE = [15, 30, 60] as const
export type IntervaloLembrete = (typeof INTERVALOS_LEMBRETE)[number]

const PREFIXO = 'acordu:lembrete-cronometro:'
const PREFIXO_LIGADO = 'acordu:lembrete-cronometro-ligado:'
const PREFIXO_ULTIMO = 'acordu:lembrete-cronometro-ultimo:'

function ler(chave: string): number | null {
    try {
        const valor = Number(window.localStorage.getItem(chave))
        return Number.isFinite(valor) && valor > 0 ? valor : null
    } catch {
        return null
    }
}

function gravar(chave: string, valor: number | null): void {
    try {
        if (valor === null) window.localStorage.removeItem(chave)
        else window.localStorage.setItem(chave, String(valor))
    } catch {
        // Sem armazenamento (navegação privada restrita): o lembrete fica desligado.
    }
}

export function intervaloLembrete(escopo: string): IntervaloLembrete | null {
    try {
        const valor = Number(window.localStorage.getItem(PREFIXO + escopo))
        return (INTERVALOS_LEMBRETE as readonly number[]).includes(valor) ? (valor as IntervaloLembrete) : null
    } catch {
        return null
    }
}

export function definirLembrete(escopo: string, intervalo: IntervaloLembrete | null, agora = Date.now()): void {
    gravar(PREFIXO + escopo, intervalo)
    // Ligar no meio do dia é ponto de partida: o primeiro aviso vem depois de um intervalo inteiro.
    gravar(PREFIXO_LIGADO + escopo, intervalo === null ? null : agora)
}

/** Quando a pessoa ligou (ou mudou o intervalo) neste navegador. */
export function lembreteLigadoEm(escopo: string): number | null {
    return ler(PREFIXO_LIGADO + escopo)
}

/** Último lembrete mostrado por qualquer aba deste navegador (F12-15). */
export function ultimoLembrete(escopo: string): number | null {
    return ler(PREFIXO_ULTIMO + escopo)
}

export function registrarLembrete(escopo: string, agora: number): void {
    gravar(PREFIXO_ULTIMO + escopo, agora)
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
