import { act, renderHook } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import manifestoBruto from '../../../public/manifest.webmanifest?raw'
import { anunciarManifesto, prepararInstalacao, reiniciarInstalacaoParaTestes, useInstalacao } from './instalacao'

function pedidoFalso() {
    const evento = new Event('beforeinstallprompt', { cancelable: true }) as Event & {
        prompt: ReturnType<typeof vi.fn>
        userChoice: Promise<{ outcome: string }>
    }
    evento.prompt = vi.fn(async () => undefined)
    evento.userChoice = Promise.resolve({ outcome: 'accepted' })
    return evento
}

describe('instalação do app (F12-26, F12-29)', () => {
    afterEach(() => {
        reiniciarInstalacaoParaTestes()
        vi.unstubAllGlobals()
        document.head.querySelectorAll('link[rel="manifest"]').forEach((l) => l.remove())
    })

    it('o pedido do navegador vira "disponível"; instalar mostra o pedido e ele some', async () => {
        prepararInstalacao(false)
        const { result } = renderHook(() => useInstalacao())
        expect(result.current.disponivel).toBe(false)

        const evento = pedidoFalso()
        act(() => {
            window.dispatchEvent(evento)
        })
        expect(evento.defaultPrevented).toBe(true)
        expect(result.current.disponivel).toBe(true)

        await act(() => result.current.instalar())
        expect(evento.prompt).toHaveBeenCalled()
        expect(result.current.disponivel).toBe(false)
    })

    it('instalado por fora (barra do navegador), o convite some', () => {
        prepararInstalacao(false)
        const { result } = renderHook(() => useInstalacao())
        act(() => {
            window.dispatchEvent(pedidoFalso())
        })
        act(() => {
            window.dispatchEvent(new Event('appinstalled'))
        })
        expect(result.current.disponivel).toBe(false)
    })

    it('registra o service worker só em produção', () => {
        const registrar = vi.fn(async () => ({}))
        vi.stubGlobal('navigator', { ...navigator, serviceWorker: { register: registrar } })
        prepararInstalacao(false)
        expect(registrar).not.toHaveBeenCalled()
        reiniciarInstalacaoParaTestes()
        prepararInstalacao(true)
        expect(registrar).toHaveBeenCalledWith('/sw.js')
    })

    it('o manifesto entra uma vez só (casca da equipe)', () => {
        anunciarManifesto()
        anunciarManifesto()
        const links = document.head.querySelectorAll('link[rel="manifest"]')
        expect(links).toHaveLength(1)
        expect(links[0]!.getAttribute('href')).toBe('/manifest.webmanifest')
    })

    it('a limpeza tira o manifesto (troca para o portal na mesma página)', () => {
        const limpar = anunciarManifesto()
        limpar()
        expect(document.head.querySelector('link[rel="manifest"]')).toBeNull()
    })

    it('chamar de novo não soma ouvintes', () => {
        prepararInstalacao(false)
        prepararInstalacao(false)
        const { result } = renderHook(() => useInstalacao())
        const evento = pedidoFalso()
        const prevenir = vi.spyOn(evento, 'preventDefault')
        act(() => {
            window.dispatchEvent(evento)
        })
        expect(prevenir).toHaveBeenCalledTimes(1)
        expect(result.current.disponivel).toBe(true)
    })
})

describe('manifesto (F12-27)', () => {
    const manifesto = JSON.parse(manifestoBruto) as {
        name: string
        start_url: string
        display: string
        icons: { src: string; sizes: string; purpose?: string }[]
    }
    const pngs = Object.keys(import.meta.glob('../../../public/icones/*.png')).map((c) =>
        c.replace('../../../public', ''),
    )

    it('abre em janela própria, na tela Hoje, com nome e ícones', () => {
        expect(manifesto.name).toBe('Acordu')
        expect(manifesto.display).toBe('standalone')
        expect(manifesto.start_url).toBe('/')
        expect(manifesto.icons.map((i) => [i.sizes, i.purpose ?? 'any'])).toEqual([
            ['192x192', 'any'],
            ['512x512', 'any'],
            ['512x512', 'maskable'],
        ])
    })

    it('os ícones do manifesto existem', () => {
        for (const icone of manifesto.icons) expect(pngs).toContain(icone.src)
        expect(pngs).toContain('/icones/apple-touch-icon.png')
    })
})
