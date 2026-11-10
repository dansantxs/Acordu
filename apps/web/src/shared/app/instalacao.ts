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

let preparada = false

function aoOferecer(evento: Event): void {
    // Sem a barra automática: o convite fica no menu da conta.
    evento.preventDefault()
    pedido = evento as PedidoInstalacao
    avisarOuvintes()
}

function aoInstalar(): void {
    pedido = null
    avisarOuvintes()
}

/** Chamado uma vez, no início do app (chamar de novo não soma ouvintes). */
export function prepararInstalacao(producao = import.meta.env.PROD): void {
    if (typeof window === 'undefined' || preparada) return
    preparada = true
    window.addEventListener('beforeinstallprompt', aoOferecer)
    window.addEventListener('appinstalled', aoInstalar)
    // Em desenvolvimento, sem service worker: o Vite serve tudo pela rede e um cache atrapalharia.
    if (producao && 'serviceWorker' in navigator) void navigator.serviceWorker.register('/sw.js').catch(() => undefined)
}

/** Só para os testes: volta ao estado de antes de `prepararInstalacao`. */
export function reiniciarInstalacaoParaTestes(): void {
    window.removeEventListener('beforeinstallprompt', aoOferecer)
    window.removeEventListener('appinstalled', aoInstalar)
    preparada = false
    pedido = null
    avisarOuvintes()
}

/**
 * Manifesto só na casca da equipe (o portal não se instala, D7): o navegador só oferece instalar depois dele. Devolve a
 * limpeza: quem troca para uma empresa onde é cliente vai para o portal na mesma página, e o manifesto sai junto.
 */
export function anunciarManifesto(): () => void {
    if (document.querySelector('link[rel="manifest"]')) return () => undefined
    const link = document.createElement('link')
    link.rel = 'manifest'
    link.href = '/manifest.webmanifest'
    document.head.appendChild(link)
    return () => link.remove()
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
