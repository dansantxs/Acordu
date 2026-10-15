import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
    abrirSessao,
    aguardarSessao,
    definirRenovador,
    encerrarSessao,
    estadoSessao,
    iniciarSessao,
    reiniciarParaTestes,
    renovar,
    tokenAtual,
    tokenVencendo,
} from './sessao'

const daquiA = (ms: number) => new Date(Date.now() + ms).toISOString()

describe('sessão', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.restoreAllMocks())

    it('recupera a sessão pelo cookie na primeira carga', async () => {
        definirRenovador(async () => ({ accessToken: 'a1', expiraEm: daquiA(3_600_000) }))
        await iniciarSessao()
        expect(estadoSessao()).toEqual({ situacao: 'ativa', motivo: 'inicio' })
        expect(tokenAtual()).toBe('a1')
    })

    it('fica anônima quando a API recusa o refresh', async () => {
        definirRenovador(async () => null)
        const espera = aguardarSessao()
        await iniciarSessao()
        expect((await espera).situacao).toBe('anonima')
        expect(tokenAtual()).toBeNull()
    })

    it('fica anônima sem rede na primeira carga, sem tentar de novo', async () => {
        const renovador = vi.fn().mockRejectedValue(new TypeError('Failed to fetch'))
        definirRenovador(renovador)
        await iniciarSessao()
        expect(estadoSessao().situacao).toBe('anonima')
        expect(renovador).toHaveBeenCalledTimes(1)
    })

    it('renovações simultâneas na aba fazem uma chamada só', async () => {
        let liberar: () => void = () => undefined
        const renovador = vi.fn(
            () =>
                new Promise<{ accessToken: string; expiraEm: string }>((resolver) => {
                    liberar = () => resolver({ accessToken: 'a2', expiraEm: daquiA(3_600_000) })
                }),
        )
        definirRenovador(renovador)
        const primeira = renovar()
        const segunda = renovar()
        liberar()
        expect(await Promise.all([primeira, segunda])).toEqual([true, true])
        expect(renovador).toHaveBeenCalledTimes(1)
    })

    it('troca e encerramento avisam quem assina, com o motivo', async () => {
        abrirSessao({ accessToken: 'a3', expiraEm: daquiA(3_600_000) })
        expect(estadoSessao()).toEqual({ situacao: 'ativa', motivo: 'troca' })
        encerrarSessao()
        expect(estadoSessao()).toEqual({ situacao: 'anonima', motivo: 'encerramento' })
        expect(tokenAtual()).toBeNull()
    })

    it('trata como vencido o token a menos de um minuto do fim', () => {
        abrirSessao({ accessToken: 'a4', expiraEm: daquiA(30_000) })
        expect(tokenVencendo()).toBe(true)
        abrirSessao({ accessToken: 'a5', expiraEm: daquiA(10 * 60_000) })
        expect(tokenVencendo()).toBe(false)
    })
})
