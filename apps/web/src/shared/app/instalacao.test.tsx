import { act, renderHook } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { anunciarManifesto, prepararInstalacao, useInstalacao } from './instalacao'

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
})
