import { useSyncExternalStore } from 'react'

/**
 * App instalável (spec do F12, F12f, decisão D7): registra o service worker e guarda o pedido de instalação que o
 * Chrome e o Edge oferecem (`beforeinstallprompt`), para o menu da conta mostrar "Instalar o Acordu" enquanto ele
 * estiver disponível (F12-26). Safari não tem esse evento: lá, a ajuda explica o caminho (F12-30).
 */

interface PedidoInstalacao extends Event {
    prompt: () => Promise<void>
    userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>
}

let pedido: PedidoInstalacao | null = null
const ouvintes = new Set<() => void>()
const avisarOuvintes = () => ouvintes.forEach((ouvinte) => ouvinte())

/** Chamado uma vez, no início do app. */
export function prepararInstalacao(producao = import.meta.env.PROD): void {
    if (typeof window === 'undefined') return
    window.addEventListener('beforeinstallprompt', (evento) => {
        // Sem a barra automática: o convite fica no menu da conta.
        evento.preventDefault()
        pedido = evento as PedidoInstalacao
        avisarOuvintes()
    })
    window.addEventListener('appinstalled', () => {
        pedido = null
        avisarOuvintes()
    })
    // Em desenvolvimento, sem service worker: o Vite serve tudo pela rede e um cache atrapalharia.
    if (producao && 'serviceWorker' in navigator) void navigator.serviceWorker.register('/sw.js').catch(() => undefined)
}

/** Manifesto só na casca da equipe (o portal não se instala, D7): o navegador só oferece instalar depois dele. */
export function anunciarManifesto(): void {
    if (document.querySelector('link[rel="manifest"]')) return
    const link = document.createElement('link')
    link.rel = 'manifest'
    link.href = '/manifest.webmanifest'
    document.head.appendChild(link)
}

const inscrever = (ouvinte: () => void) => {
    ouvintes.add(ouvinte)
    return () => ouvintes.delete(ouvinte)
}

export function useInstalacao(): { disponivel: boolean; instalar: () => Promise<void> } {
    const atual = useSyncExternalStore(inscrever, () => pedido)
    return {
        disponivel: atual !== null,
        instalar: async () => {
            if (!pedido) return
            const usado = pedido
            pedido = null
            avisarOuvintes()
            await usado.prompt()
        },
    }
}
