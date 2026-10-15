import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { abrirSessao, estadoSessao, reiniciarParaTestes, tokenAtual } from '../sessao/sessao'
import { api } from './cliente'
import { ErroApi } from './problema'

const daquiA = (ms: number) => new Date(Date.now() + ms).toISOString()
const json = (corpo: unknown, status = 200) =>
    new Response(JSON.stringify(corpo), { status, headers: { 'content-type': 'application/json' } })

/** Responde por caminho; registra o Authorization de cada chamada. */
function servidor(rotas: Record<string, (() => Response)[]>) {
    const chamadas: { caminho: string; autorizacao: string | null }[] = []
    vi.stubGlobal(
        'fetch',
        vi.fn(async (requisicao: Request) => {
            const caminho = new URL(requisicao.url).pathname
            chamadas.push({ caminho, autorizacao: requisicao.headers.get('Authorization') })
            const fila = rotas[caminho]
            const proxima = fila?.shift()
            if (!proxima) throw new Error(`Sem resposta para ${caminho}`)
            return proxima()
        }),
    )
    return chamadas
}

const perfil = { usuarioId: 'u', nome: 'Ana' }

describe('cliente HTTP', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('envia o access token em memória', async () => {
        abrirSessao({ accessToken: 't1', expiraEm: daquiA(3_600_000) })
        const chamadas = servidor({ '/api/v1/auth/eu': [() => json(perfil)] })
        await api.GET('/api/v1/auth/eu')
        expect(chamadas[0]?.autorizacao).toBe('Bearer t1')
    })

    it('renova antes de enviar quando o token está vencendo', async () => {
        abrirSessao({ accessToken: 'velho', expiraEm: daquiA(5_000) })
        const chamadas = servidor({
            '/api/v1/auth/refresh': [() => json({ accessToken: 'novo', expiraEm: daquiA(3_600_000) })],
            '/api/v1/auth/eu': [() => json(perfil)],
        })
        await api.GET('/api/v1/auth/eu')
        expect(chamadas.map((c) => c.caminho)).toEqual(['/api/v1/auth/refresh', '/api/v1/auth/eu'])
        expect(chamadas[0]?.autorizacao).toBeNull()
        expect(chamadas[1]?.autorizacao).toBe('Bearer novo')
    })

    it('depois de um 401, renova e repete uma vez', async () => {
        abrirSessao({ accessToken: 'recusado', expiraEm: daquiA(3_600_000) })
        const chamadas = servidor({
            '/api/v1/auth/eu': [() => json({ status: 401 }, 401), () => json(perfil)],
            '/api/v1/auth/refresh': [() => json({ accessToken: 'aceito', expiraEm: daquiA(3_600_000) })],
        })
        const { data } = await api.GET('/api/v1/auth/eu')
        expect(data).toMatchObject(perfil)
        expect(chamadas.map((c) => c.autorizacao)).toEqual(['Bearer recusado', null, 'Bearer aceito'])
    })

    it('encerra a sessão quando o refresh também é recusado', async () => {
        abrirSessao({ accessToken: 't2', expiraEm: daquiA(3_600_000) })
        servidor({
            '/api/v1/auth/eu': [() => json({ status: 401 }, 401)],
            '/api/v1/auth/refresh': [() => new Response(null, { status: 401 })],
        })
        await expect(api.GET('/api/v1/auth/eu')).rejects.toBeInstanceOf(ErroApi)
        expect(estadoSessao().situacao).toBe('anonima')
        expect(tokenAtual()).toBeNull()
    })

    it('não renova no 401 do login: é a resposta', async () => {
        const chamadas = servidor({
            '/api/v1/auth/login': [
                () =>
                    json(
                        {
                            type: 'https://acordu.app.br/erros/nao-autenticado',
                            status: 401,
                            detail: 'E-mail ou senha inválidos.',
                        },
                        401,
                    ),
            ],
        })
        const falha = api.POST('/api/v1/auth/login', { body: { email: 'a@b.c', senha: 'x' } })
        await expect(falha).rejects.toMatchObject({ status: 401, message: 'E-mail ou senha inválidos.' })
        expect(chamadas).toHaveLength(1)
    })
})
