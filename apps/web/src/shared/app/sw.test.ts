import { beforeEach, describe, expect, it, vi } from 'vitest'
import codigo from '../../../public/sw.js?raw'

/** Carrega o public/sw.js num `self` falso e devolve os ouvintes registrados por ele. */
function carregar() {
    const ouvintes: Record<string, (evento: unknown) => void> = {}
    const guardados = new Map<string, string[]>()
    const caches = {
        open: vi.fn(async (nome: string) => ({
            addAll: vi.fn(async (urls: string[]) => void guardados.set(nome, urls)),
        })),
        keys: vi.fn(async () => ['acordu-casca-0', 'acordu-casca-1']),
        delete: vi.fn(async () => true),
        match: vi.fn(async () => 'pagina-offline'),
    }
    const janela = { url: 'https://acordu.app.br/hoje', focus: vi.fn(), navigate: vi.fn() }
    janela.focus.mockResolvedValue(janela)
    const self = {
        addEventListener: (tipo: string, fn: (evento: unknown) => void) => (ouvintes[tipo] = fn),
        skipWaiting: vi.fn(),
        location: { origin: 'https://acordu.app.br' },
        clients: { claim: vi.fn(), matchAll: vi.fn(async () => [janela]), openWindow: vi.fn() },
    }
    const buscar = vi.fn()
    new Function('self', 'caches', 'fetch', codigo)(self, caches, buscar)
    return { ouvintes, guardados, caches, self, buscar, janela }
}

describe('service worker (F12-28, F12-29)', () => {
    let sw: ReturnType<typeof carregar>
    beforeEach(() => {
        sw = carregar()
    })

    it('na instalação guarda só a casca offline e assume na hora', async () => {
        let espera: Promise<unknown> = Promise.resolve()
        sw.ouvintes.install!({ waitUntil: (p: Promise<unknown>) => (espera = p) })
        await espera
        expect([...sw.guardados.values()]).toEqual([['/offline.html', '/offline.css', '/icones/icone-192.png']])
        expect(sw.self.skipWaiting).toHaveBeenCalled()
    })

    it('ao ativar, apaga as casas de versões velhas', async () => {
        let espera: Promise<unknown> = Promise.resolve()
        sw.ouvintes.activate!({ waitUntil: (p: Promise<unknown>) => (espera = p) })
        await espera
        expect(sw.caches.delete).toHaveBeenCalledWith('acordu-casca-0')
        expect(sw.caches.delete).not.toHaveBeenCalledWith('acordu-casca-1')
        expect(sw.self.clients.claim).toHaveBeenCalled()
    })

    it('navegação vai à rede; sem rede, mostra "Sem conexão"', async () => {
        sw.buscar.mockRejectedValueOnce(new TypeError('Failed to fetch'))
        let resposta: Promise<unknown> = Promise.resolve()
        sw.ouvintes.fetch!({ request: { mode: 'navigate', url: 'https://acordu.app.br/tarefas/x' }, respondWith: (p: Promise<unknown>) => (resposta = p) })
        expect(await resposta).toBe('pagina-offline')
        expect(sw.buscar).toHaveBeenCalled()
    })

    it('API e arquivos do app não passam pelo service worker (nada de dado em cache)', () => {
        const responder = vi.fn()
        sw.ouvintes.fetch!({
            request: { mode: 'cors', url: 'https://api.acordu.app.br/api/v1/tarefas' },
            respondWith: responder,
        })
        sw.ouvintes.fetch!({ request: { mode: 'no-cors', url: 'https://acordu.app.br/assets/index.js' }, respondWith: responder })
        sw.ouvintes.fetch!({ request: { mode: 'no-cors', url: 'https://outro.site/offline.css' }, respondWith: responder })
        expect(responder).not.toHaveBeenCalled()
        expect(sw.caches.match).not.toHaveBeenCalled()
    })

    it('sem rede, o estilo e o ícone da página "Sem conexão" vêm do cache', async () => {
        sw.buscar.mockRejectedValue(new TypeError('Failed to fetch'))
        for (const caminho of ['/offline.css', '/icones/icone-192.png']) {
            let resposta: Promise<unknown> = Promise.resolve()
            sw.ouvintes.fetch!({
                request: { mode: 'no-cors', url: `https://acordu.app.br${caminho}` },
                respondWith: (p: Promise<unknown>) => (resposta = p),
            })
            expect(await resposta).toBe('pagina-offline')
            expect(sw.caches.match).toHaveBeenLastCalledWith(caminho, { cacheName: 'acordu-casca-1' })
        }
    })

    it('clique na notificação com caminho de fora abre só o início', async () => {
        for (const caminho of ['//mal.site/x', 'https://mal.site/x', '/\\mal.site']) {
            let espera: Promise<unknown> = Promise.resolve()
            sw.ouvintes.notificationclick!({
                notification: { close: vi.fn(), data: { caminho } },
                waitUntil: (p: Promise<unknown>) => (espera = p),
            })
            await espera
            expect(sw.janela.navigate).toHaveBeenLastCalledWith('/')
        }
    })

    it('clique na notificação foca a janela aberta e leva ao item', async () => {
        const fechar = vi.fn()
        let espera: Promise<unknown> = Promise.resolve()
        sw.ouvintes.notificationclick!({
            notification: { close: fechar, data: { caminho: '/tarefas/t1' } },
            waitUntil: (p: Promise<unknown>) => (espera = p),
        })
        await espera
        expect(fechar).toHaveBeenCalled()
        expect(sw.janela.navigate).toHaveBeenCalledWith('/tarefas/t1')
    })
})
