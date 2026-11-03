import { describe, expect, it, vi } from 'vitest'
import { CABECALHO_TOKEN, repassar, type Env } from './index'

const env: Env = { ORIGEM: 'https://acordu-api-xyz-uk.a.run.app', TOKEN_ORIGEM: 'segredo-da-borda' }

/** Repassa a requisição a um `fetch` falso e devolve o que chegou à origem. */
async function naOrigem(requisicao: Request, resposta = new Response('ok')) {
    const buscar = vi.fn(async (_url: URL | RequestInfo, _init?: RequestInit) => resposta)
    const devolvida = await repassar(requisicao, env, buscar as unknown as typeof fetch)
    const [url, init] = buscar.mock.calls[0]!
    return { url: String(url), init: init!, cabecalhos: new Headers(init!.headers), devolvida }
}

describe('borda da API (spec do F8)', () => {
    it('F8-23: repassa método, caminho, query e cabeçalhos para a origem, com o token', async () => {
        const { url, init, cabecalhos } = await naOrigem(
            new Request('https://api.acordu.app.br/api/v1/solicitacoes?status=ABERTA&pagina=2', {
                headers: { Authorization: 'Bearer abc', 'If-Match': '"7"' },
            }),
        )

        expect(url).toBe('https://acordu-api-xyz-uk.a.run.app/api/v1/solicitacoes?status=ABERTA&pagina=2')
        expect(init.method).toBe('GET')
        expect(init.body).toBeNull()
        expect(cabecalhos.get('Authorization')).toBe('Bearer abc')
        expect(cabecalhos.get('If-Match')).toBe('"7"')
        expect(cabecalhos.get(CABECALHO_TOKEN)).toBe('segredo-da-borda')
    })

    it('F8-23: o corpo vai em stream, sem ser lido pela borda', async () => {
        const requisicao = new Request('https://api.acordu.app.br/api/v1/solicitacoes/1/interacoes', {
            method: 'POST',
            body: 'conteúdo grande',
            headers: { 'Content-Type': 'multipart/form-data; boundary=x' },
        })
        const { init, cabecalhos } = await naOrigem(requisicao)

        expect(init.method).toBe('POST')
        expect(init.body).toBe(requisicao.body)
        expect(requisicao.bodyUsed).toBe(false)
        expect(cabecalhos.get('Content-Type')).toBe('multipart/form-data; boundary=x')
    })

    it('F8-24: X-Forwarded-For vira só o IP que o Cloudflare viu; o do cliente e um token forjado são descartados', async () => {
        const { cabecalhos } = await naOrigem(
            new Request('https://api.acordu.app.br/api/v1/auth/login', {
                method: 'POST',
                body: '{}',
                headers: {
                    'CF-Connecting-IP': '203.0.113.7',
                    'X-Forwarded-For': '1.2.3.4',
                    [CABECALHO_TOKEN]: 'forjado',
                },
            }),
        )

        expect(cabecalhos.get('X-Forwarded-For')).toBe('203.0.113.7')
        expect(cabecalhos.get(CABECALHO_TOKEN)).toBe('segredo-da-borda')
    })

    it('F8-25: sem cache, redirecionamento devolvido como veio e a resposta da origem passa intacta (stream)', async () => {
        const fluxo = new Response('event: mudanca\n\n', { headers: { 'Content-Type': 'text/event-stream' } })
        const { init, devolvida } = await naOrigem(new Request('https://api.acordu.app.br/api/v1/eventos'), fluxo)

        expect(init.cache).toBe('no-store')
        expect(init.redirect).toBe('manual')
        expect(devolvida).toBe(fluxo)
    })

    it('sem configuração, recusa em vez de repassar sem o token', async () => {
        const buscar = vi.fn()
        const resposta = await repassar(
            new Request('https://api.acordu.app.br/api/health'),
            { ORIGEM: '', TOKEN_ORIGEM: '' },
            buscar as unknown as typeof fetch,
        )

        expect(resposta.status).toBe(500)
        expect(buscar).not.toHaveBeenCalled()
    })
})
