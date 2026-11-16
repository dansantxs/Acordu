import { describe, expect, it } from 'vitest'
import {
    baseDaApi,
    contatoNebrys,
    intervaloDePolling,
    modoTempoReal,
    textosLegaisPublicados,
    versaoDoBuild,
} from './ambiente'

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

    it('F13-21: sem VITE_VERSAO não há versão; o commit inteiro vira o curto, como na API', () => {
        expect(versaoDoBuild(undefined)).toBeUndefined()
        expect(versaoDoBuild('  ')).toBeUndefined()
        expect(versaoDoBuild('a62cfef1234567890abcdef1234567890abcdef1')).toBe('a62cfef')
        expect(versaoDoBuild('1.2.3')).toBe('1.2.3')
    })

    it('ContatoNebrys_ComESemAVariavel_EnderecoAparadoOuNenhum (F14, D15)', () => {
        expect(contatoNebrys(' contato@acordu.app.br ')).toBe('contato@acordu.app.br')
        expect(contatoNebrys(undefined)).toBeUndefined()
        expect(contatoNebrys('  ')).toBeUndefined()
    })

    it('TextosLegaisPublicados_ComESemAVariavel_SoTrueLiga (F14f, #194)', () => {
        expect(textosLegaisPublicados('true')).toBe(true)
        expect(textosLegaisPublicados(' TRUE ')).toBe(true)
        expect(textosLegaisPublicados(undefined)).toBe(false)
        expect(textosLegaisPublicados('')).toBe(false)
        expect(textosLegaisPublicados('false')).toBe(false)
        expect(textosLegaisPublicados('1')).toBe(false)
    })
})
