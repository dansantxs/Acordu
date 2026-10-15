import { QueryClient } from '@tanstack/react-query'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { abrirSessao, definirRenovador, reiniciarParaTestes } from '../sessao/sessao'
import { conectarTempoReal } from './conexao'

const daquiA = (ms: number) => new Date(Date.now() + ms).toISOString()

/** Resposta SSE que entrega os pedaços e fecha (ou fica aberta, se `manterAberta`). */
function fluxo(pedacos: string[], manterAberta = false) {
    const codificador = new TextEncoder()
    return new Response(
        new ReadableStream({
            start(controle) {
                pedacos.forEach((p) => controle.enqueue(codificador.encode(p)))
                if (!manterAberta) controle.close()
            },
        }),
        { status: 200, headers: { 'content-type': 'text/event-stream' } },
    )
}

const sinal = (tipo: string, id: string) => `event: mudanca\ndata: {"tipo":"${tipo}","id":"${id}"}\n\n`

describe('tempo real', () => {
    let parar: () => void = () => undefined
    beforeEach(() => {
        reiniciarParaTestes()
        abrirSessao({ accessToken: 'tok', expiraEm: daquiA(3_600_000) })
    })
    afterEach(() => {
        parar()
        vi.unstubAllGlobals()
    })

    it('sinais próximos viram uma invalidação do recurso e das visões, com o Bearer', async () => {
        const consultas = new QueryClient()
        const invalidar = vi.spyOn(consultas, 'invalidateQueries')
        const chamadas: Request[] = []
        vi.stubGlobal(
            'fetch',
            vi.fn(async (r: Request) => {
                chamadas.push(r)
                return fluxo(
                    [': vivo\n\n', sinal('tarefa', 't1'), sinal('tarefa', 't2') + sinal('cliente', 'c1')],
                    true,
                )
            }),
        )
        parar = conectarTempoReal({ consultas })
        await vi.waitFor(() => expect(invalidar).toHaveBeenCalled())
        const chaves = invalidar.mock.calls.map(([filtro]) => filtro?.queryKey?.[0])
        expect(chaves).toEqual([
            'tarefas',
            'clientes',
            'agenda',
            'capacidade',
            'panorama',
            'atividades',
            'hoje',
            'paleta',
        ])
        expect(chamadas[0]?.headers.get('Authorization')).toBe('Bearer tok')
    })

    it('ao reconectar depois de uma queda, invalida tudo (sinais perdidos)', async () => {
        const consultas = new QueryClient()
        const invalidar = vi.spyOn(consultas, 'invalidateQueries')
        let conexoes = 0
        vi.stubGlobal(
            'fetch',
            vi.fn(async () => (++conexoes === 1 ? fluxo([': vivo\n\n']) : fluxo([], true))),
        )
        parar = conectarTempoReal({ consultas, esperar: async () => undefined })
        await vi.waitFor(() => expect(conexoes).toBe(2))
        await vi.waitFor(() => expect(invalidar).toHaveBeenCalledWith())
    })

    it('com 401, renova o token e reconecta com o novo', async () => {
        definirRenovador(async () => ({ accessToken: 'novo', expiraEm: daquiA(3_600_000) }))
        const autorizacoes: (string | null)[] = []
        vi.stubGlobal(
            'fetch',
            vi.fn(async (r: Request) => {
                autorizacoes.push(r.headers.get('Authorization'))
                return autorizacoes.length === 1 ? new Response(null, { status: 401 }) : fluxo([], true)
            }),
        )
        parar = conectarTempoReal({ consultas: new QueryClient(), esperar: async () => undefined })
        await vi.waitFor(() => expect(autorizacoes).toEqual(['Bearer tok', 'Bearer novo']))
    })

    it('com 403 (contato de cliente), desiste', async () => {
        const buscar = vi.fn(async () => new Response(null, { status: 403 }))
        vi.stubGlobal('fetch', buscar)
        parar = conectarTempoReal({ consultas: new QueryClient(), esperar: async () => undefined })
        await new Promise((r) => setTimeout(r, 50))
        expect(buscar).toHaveBeenCalledTimes(1)
    })
})
