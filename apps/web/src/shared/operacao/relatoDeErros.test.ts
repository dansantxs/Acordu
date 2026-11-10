import { afterEach, describe, expect, it, vi } from 'vitest'
import { ErroApi } from '../api/problema'
import { esquecerRelatados, instalarRelatoDeErros, montarRelato, relatarErro, rotaModelo } from './relatoDeErros'

vi.mock('../config/ambiente', async (original) => ({
    ...(await original<typeof import('../config/ambiente')>()),
    BASE_API: 'https://api.acordu.app.br/',
    VERSAO: 'abc1234',
}))

describe('relato de erros do app (F13d)', () => {
    afterEach(() => {
        esquecerRelatados()
        vi.unstubAllGlobals()
    })

    it('rota modelo: ids, números e trechos longos viram :id', () => {
        expect(rotaModelo('/clientes/7c1f0e2a-1b2c-4d5e-8f90-a1b2c3d4e5f6/contratos')).toBe('/clientes/:id/contratos')
        expect(rotaModelo('/portal/solicitacoes/42')).toBe('/portal/solicitacoes/:id')
        expect(rotaModelo(`/link/${'t'.repeat(60)}`)).toBe('/link/:id')
    })

    it('monta o relato com mensagem, pilha, rota modelo, versão e navegador (F13-15)', () => {
        const erro = new TypeError('x.y is not a function')
        const relato = montarRelato(erro, 'abc1234', '/tarefas/7c1f0e2a-1b2c-4d5e-8f90-a1b2c3d4e5f6')
        expect(relato).toMatchObject({
            mensagem: 'TypeError: x.y is not a function',
            rota: '/tarefas/:id',
            versao: 'abc1234',
        })
        expect(relato?.pilha).toContain('x.y is not a function')
        expect(relato?.navegador).toBe(navigator.userAgent.slice(0, 200))
    })

    it('resposta da API e falta de rede não são relatadas (F13-17)', () => {
        expect(montarRelato(new ErroApi(500, { title: 'Erro interno' }), 'abc1234', '/')).toBeNull()
        expect(montarRelato(new TypeError('Failed to fetch'), 'abc1234', '/')).toBeNull()
        expect(
            montarRelato(new TypeError('NetworkError when attempting to fetch resource.'), 'abc1234', '/'),
        ).toBeNull()
    })

    it('sem versão (desenvolvimento e testes) nada é relatado (F13-21)', () => {
        expect(montarRelato(new Error('x'), null, '/')).toBeNull()
    })

    it('manda à rota pública sem cookie, uma vez por erro e por carga de página (F13-16)', () => {
        const enviar = vi.fn().mockResolvedValue(new Response(null, { status: 204 }))
        const erro = new Error('quebrou')

        expect(relatarErro(erro, enviar)).toBe(true)
        expect(relatarErro(erro, enviar)).toBe(false)

        expect(enviar).toHaveBeenCalledTimes(1)
        const [url, opcoes] = enviar.mock.calls[0] as [string, RequestInit]
        expect(url).toBe('https://api.acordu.app.br/api/v1/erros-web')
        expect(opcoes).toMatchObject({ method: 'POST', credentials: 'omit', keepalive: true })
        expect(opcoes.headers).toEqual({ 'Content-Type': 'text/plain' })
        expect(JSON.parse(opcoes.body as string)).toMatchObject({ mensagem: 'Error: quebrou', versao: 'abc1234' })
        expect(opcoes.headers).not.toHaveProperty('Authorization')
    })

    it('não relata o que não é defeito: Script error, AbortError, chunk de um deploy antigo', () => {
        expect(montarRelato(new Error('Script error.'), 'abc1234', '/')).toBeNull()
        expect(montarRelato('Script error.', 'abc1234', '/')).toBeNull()
        expect(montarRelato(new DOMException('cancelada', 'AbortError'), 'abc1234', '/')).toBeNull()
        expect(
            montarRelato(new TypeError('error loading dynamically imported module: https://x/a.js'), 'abc1234', '/'),
        ).toBeNull()
    })

    it('no máximo 10 relatos por carga de página', () => {
        const enviar = vi.fn().mockResolvedValue(new Response(null, { status: 204 }))
        for (let i = 0; i < 15; i++) relatarErro(new Error(`erro ${i}`), enviar)
        expect(enviar).toHaveBeenCalledTimes(10)
    })

    it('falha ao relatar não vira outro erro', () => {
        const enviar = vi.fn().mockRejectedValue(new TypeError('Failed to fetch'))
        expect(() => relatarErro(new Error('outro'), enviar)).not.toThrow()
    })

    it('erros da janela (error e unhandledrejection) são relatados (F13-16)', () => {
        const enviar = vi.fn().mockResolvedValue(new Response(null, { status: 204 }))
        vi.stubGlobal('fetch', enviar)
        const janela = new EventTarget() as unknown as Window
        instalarRelatoDeErros(janela)

        janela.dispatchEvent(Object.assign(new Event('error'), { error: new Error('na janela') }))
        janela.dispatchEvent(Object.assign(new Event('unhandledrejection'), { reason: new Error('promessa') }))

        expect(enviar).toHaveBeenCalledTimes(2)
    })
})
