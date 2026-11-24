/**
 * Tokens de link (convite da equipe, do portal e de empresa, redefinição de senha, confirmação de e-mail) saem da URL na primeira carga (segurança
 * §9: não ficam no histórico nem vão em Referer). Ficam na `sessionStorage` da aba, para a página sobreviver a uma
 * recarga, e são apagados quando usados.
 */
export type TipoDeLink = 'convite' | 'convite-portal' | 'convite-empresa' | 'senha' | 'confirmacao'

const memoria = new Map<TipoDeLink, string>()
const chave = (tipo: TipoDeLink) => `acordu.link.${tipo}`

export function guardarTokenDeLink(tipo: TipoDeLink, token: string): void {
    memoria.set(tipo, token)
    try {
        sessionStorage.setItem(chave(tipo), token)
    } catch {
        // Sem storage (modo restrito): fica só na memória.
    }
}

export function lerTokenDeLink(tipo: TipoDeLink): string | null {
    try {
        return sessionStorage.getItem(chave(tipo)) ?? memoria.get(tipo) ?? null
    } catch {
        return memoria.get(tipo) ?? null
    }
}

export function descartarTokenDeLink(tipo: TipoDeLink): void {
    memoria.delete(tipo)
    try {
        sessionStorage.removeItem(chave(tipo))
    } catch {
        // idem
    }
}
