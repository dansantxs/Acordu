import { describe, expect, it, vi } from 'vitest'
import { ALVO_PADRAO, CHAVE, checar, horaDeBrasilia, type Env, type Estado } from './index'

/** KV de mentira: guarda o valor e conta as gravações (o plano gratuito tem um teto por dia). */
function kv(inicial?: Estado) {
    let valor: string | null = inicial ? JSON.stringify(inicial) : null
    const gravacoes = vi.fn()
    const ns = {
        get: vi.fn(async (_chave: string, _tipo: 'json') => (valor ? JSON.parse(valor) : null)),
        put: vi.fn(async (chave: string, novo: string) => {
            expect(chave).toBe(CHAVE)
            valor = novo
            gravacoes()
        }),
    }
    return { ns: ns as unknown as KVNamespace, gravacoes, valor: () => (valor ? (JSON.parse(valor) as Estado) : null) }
}

const WEBHOOK = 'https://discord.com/api/webhooks/1/alertas'

/** `fetch` falso: o health responde `saude` (status ou erro); o Discord aceita, a não ser que `discordFora`. */
function rede(saude: number | 'tempo', discordFora = false) {
    const avisos: string[] = []
    const buscar = vi.fn(async (url: RequestInfo | URL, init?: RequestInit) => {
        if (String(url) === WEBHOOK) {
            if (discordFora) throw new TypeError('fetch failed')
            const corpo = JSON.parse(String(init?.body)) as { content: string; allowed_mentions: { parse: string[] } }
            expect(corpo.allowed_mentions.parse).toEqual([])
            avisos.push(corpo.content)
            return new Response(null, { status: 204 })
        }
        expect(String(url)).toBe(ALVO_PADRAO)
        if (saude === 'tempo') throw new DOMException('timeout', 'TimeoutError')
        return new Response('ok', { status: saude })
    })
    return { buscar: buscar as unknown as typeof fetch, avisos }
}

const AS_14 = new Date('2026-11-10T17:00:00Z') // 14:00 em Brasília
const depois = (minutos: number) => new Date(AS_14.getTime() + minutos * 60_000)

describe('vigia da API (F13h)', () => {
    it('F13-34: duas falhas seguidas avisam "fora do ar" com a hora da primeira, uma vez', async () => {
        const estado = kv()
        const env: Env = { ESTADO: estado.ns, DISCORD_ALERTAS: WEBHOOK }
        const fora = rede(503)

        await checar(env, AS_14, fora.buscar)
        expect(fora.avisos).toEqual([])
        await checar(env, depois(5), fora.buscar)
        await checar(env, depois(10), rede('tempo').buscar)
        await checar(env, depois(15), fora.buscar)

        expect(fora.avisos).toEqual(['**API fora do ar** desde 14:00 (Brasília).'])
        expect(estado.gravacoes).toHaveBeenCalledTimes(2) // 1ª falha e o aviso; o resto da queda não grava
    })

    it('F13-34: passar de 15 s conta como falha', async () => {
        const estado = kv({ falhas: 1, foraDesde: AS_14.toISOString(), avisada: false })
        const lenta = rede('tempo')

        await checar({ ESTADO: estado.ns, DISCORD_ALERTAS: WEBHOOK }, depois(5), lenta.buscar)

        expect(lenta.avisos).toEqual(['**API fora do ar** desde 14:00 (Brasília).'])
    })

    it('F13-35: a volta avisa quanto tempo ficou fora e zera o estado', async () => {
        const estado = kv({ falhas: 2, foraDesde: AS_14.toISOString(), avisada: true })
        const ok = rede(200)

        await checar({ ESTADO: estado.ns, DISCORD_ALERTAS: WEBHOOK }, depois(23), ok.buscar)

        expect(ok.avisos).toEqual(['**API voltou** · fora por 23 min.'])
        expect(estado.valor()).toEqual({ falhas: 0, avisada: false })
    })

    it('F13-36: uma falha e a volta (partida a frio) não avisam nada', async () => {
        const estado = kv()
        const env: Env = { ESTADO: estado.ns, DISCORD_ALERTAS: WEBHOOK }
        const fora = rede(500)
        const ok = rede(200)

        await checar(env, AS_14, fora.buscar)
        await checar(env, depois(5), ok.buscar)

        expect([...fora.avisos, ...ok.avisos]).toEqual([])
        expect(estado.valor()).toEqual({ falhas: 0, avisada: false })
    })

    it('tudo em pé: não grava no KV', async () => {
        const estado = kv()

        await checar({ ESTADO: estado.ns, DISCORD_ALERTAS: WEBHOOK }, AS_14, rede(200).buscar)

        expect(estado.gravacoes).not.toHaveBeenCalled()
    })

    it('Discord fora na queda: tenta de novo na próxima checagem', async () => {
        const estado = kv({ falhas: 1, foraDesde: AS_14.toISOString(), avisada: false })
        const env: Env = { ESTADO: estado.ns, DISCORD_ALERTAS: WEBHOOK }

        await checar(env, depois(5), rede(503, true).buscar)
        expect(estado.valor()?.avisada).toBe(false)
        const deNovo = rede(503)
        await checar(env, depois(10), deNovo.buscar)

        expect(deNovo.avisos).toEqual(['**API fora do ar** desde 14:00 (Brasília).'])
    })

    it('Discord fora na volta: a volta é avisada na próxima checagem', async () => {
        const estado = kv({ falhas: 2, foraDesde: AS_14.toISOString(), avisada: true })
        const env: Env = { ESTADO: estado.ns, DISCORD_ALERTAS: WEBHOOK }

        await checar(env, depois(20), rede(200, true).buscar)
        const ok = rede(200)
        await checar(env, depois(25), ok.buscar)

        expect(ok.avisos).toEqual(['**API voltou** · fora por 25 min.'])
    })

    it('sem webhook: só o log, e a queda continua para avisar quando houver', async () => {
        const estado = kv({ falhas: 1, foraDesde: AS_14.toISOString(), avisada: false })
        const log = vi.spyOn(console, 'log').mockImplementation(() => undefined)

        await checar({ ESTADO: estado.ns }, depois(5), rede(503).buscar)

        expect(log).toHaveBeenCalledWith(expect.stringContaining('API fora do ar'))
        expect(estado.valor()?.avisada).toBe(false)
        log.mockRestore()
    })

    it('a hora é a de Brasília', () => {
        expect(horaDeBrasilia(new Date('2026-11-10T02:30:00Z'))).toBe('23:30')
    })
})
