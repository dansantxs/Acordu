/** Tema claro ou escuro. A escolha fica no navegador; sem escolha, vale a do sistema. */
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

/**
 * Couro da capa e do acento (decidido em 23/10): Café é o padrão; Conhaque e Bordô são escolha de cada pessoa, guardada
 * no navegador. Só no app da equipe: o portal mostra o prestador e fica sempre no Café.
 */
export type Couro = 'cafe' | 'conhaque' | 'bordo'

export const COUROS: { id: Couro; nome: string }[] = [
    { id: 'cafe', nome: 'Café' },
    { id: 'conhaque', nome: 'Conhaque' },
    { id: 'bordo', nome: 'Bordô' },
]

const CHAVE_COURO = 'acordu.couro'

export function couroAtual(): Couro {
    try {
        const valor = localStorage.getItem(CHAVE_COURO)
        return valor === 'conhaque' || valor === 'bordo' ? valor : 'cafe'
    } catch {
        return 'cafe'
    }
}

/** Café é o padrão do CSS: sem atributo. `null` tira o couro (portal). */
export function aplicarCouro(couro: Couro | null): void {
    if (couro === null || couro === 'cafe') delete document.documentElement.dataset.couro
    else document.documentElement.dataset.couro = couro
}

export function escolherCouro(couro: Couro): Couro {
    try {
        if (couro === 'cafe') localStorage.removeItem(CHAVE_COURO)
        else localStorage.setItem(CHAVE_COURO, couro)
    } catch {
        // Armazenamento bloqueado: aplica só nesta página.
    }
    aplicarCouro(couro)
    return couro
}
