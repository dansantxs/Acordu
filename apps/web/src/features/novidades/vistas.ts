import { useSyncExternalStore } from 'react'
import { notasDo, type NotaVersao } from './notas'

/**
 * "Já vi" das novidades (M4): fica no navegador, por público, como a data da nota mais nova que a pessoa abriu. Sem
 * armazenamento (aba privada, bloqueio), o ponto aparece até a tela ser aberta nesta visita.
 */
const CHAVE = (publico: NotaVersao['publico']) => `acordu.novidades.${publico}`
const EVENTO = 'acordu:novidades'
const naMemoria = new Map<string, string>()

function lerVista(publico: NotaVersao['publico']): string {
    try {
        return localStorage.getItem(CHAVE(publico)) ?? naMemoria.get(publico) ?? ''
    } catch {
        return naMemoria.get(publico) ?? ''
    }
}

const maisNova = (publico: NotaVersao['publico']) => notasDo(publico).reduce((m, n) => (n.data > m ? n.data : m), '')

export function marcarVistas(publico: NotaVersao['publico']) {
    const data = maisNova(publico)
    if (!data || lerVista(publico) >= data) return
    naMemoria.set(publico, data)
    try {
        localStorage.setItem(CHAVE(publico), data)
    } catch {
        // Só na memória: vale até recarregar.
    }
    window.dispatchEvent(new Event(EVENTO))
}

function assinar(avisar: () => void) {
    window.addEventListener(EVENTO, avisar)
    window.addEventListener('storage', avisar)
    return () => {
        window.removeEventListener(EVENTO, avisar)
        window.removeEventListener('storage', avisar)
    }
}

/** Há nota mais nova do que a última vista? */
export function useHaNovidade(publico: NotaVersao['publico']): boolean {
    return useSyncExternalStore(assinar, () => maisNova(publico) > lerVista(publico))
}

export function reiniciarVistasParaTestes() {
    naMemoria.clear()
    try {
        localStorage.removeItem(CHAVE('equipe'))
        localStorage.removeItem(CHAVE('cliente'))
    } catch {
        // sem armazenamento
    }
}
