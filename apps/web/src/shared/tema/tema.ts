/** Tema claro ou escuro (couro Café). A escolha fica no navegador; sem escolha, vale a do sistema. */
export type Tema = 'claro' | 'escuro'

const CHAVE = 'acordu.tema'

function lerGuardado(): Tema | null {
    try {
        const valor = localStorage.getItem(CHAVE)
        return valor === 'claro' || valor === 'escuro' ? valor : null
    } catch {
        return null
    }
}

export function temaDoSistema(): Tema {
    return typeof window !== 'undefined' && window.matchMedia?.('(prefers-color-scheme: dark)').matches
        ? 'escuro'
        : 'claro'
}

export function temaAtual(): Tema {
    return lerGuardado() ?? temaDoSistema()
}

export function aplicarTema(tema: Tema): void {
    document.documentElement.dataset.tema = tema
}

/** Guarda e aplica; `null` volta a seguir o sistema. */
export function escolherTema(tema: Tema | null): Tema {
    try {
        if (tema === null) localStorage.removeItem(CHAVE)
        else localStorage.setItem(CHAVE, tema)
    } catch {
        // Navegação privada ou armazenamento bloqueado: aplica só nesta página.
    }
    const efetivo = tema ?? temaDoSistema()
    aplicarTema(efetivo)
    return efetivo
}
