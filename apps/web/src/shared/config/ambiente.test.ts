import { describe, expect, it } from 'vitest'
import { baseDaApi, intervaloDePolling, modoTempoReal } from './ambiente'

describe('ambiente do SPA (spec do F8)', () => {
    it('F8-17: com VITE_URL_API a base é a da API; sem ela, a origem do SPA', () => {
        expect(baseDaApi('https://api.acordu.app.br', 'https://acordu.app.br')).toBe('https://api.acordu.app.br/')
        expect(baseDaApi('https://api.acordu.app.br/', 'https://acordu.app.br')).toBe('https://api.acordu.app.br/')
        expect(baseDaApi(undefined, 'http://localhost:5173')).toBe('http://localhost:5173/')
        expect(baseDaApi('  ', 'http://localhost:5173')).toBe('http://localhost:5173/')
    })

    it('F8-20: polling recarrega a cada 30 s; SSE (padrão) não recarrega por tempo', () => {
        expect(modoTempoReal('polling')).toBe('polling')
        expect(modoTempoReal(undefined)).toBe('sse')
        expect(modoTempoReal('qualquer')).toBe('sse')
        expect(intervaloDePolling('polling')).toBe(30_000)
        expect(intervaloDePolling('sse')).toBe(false)
    })
})
