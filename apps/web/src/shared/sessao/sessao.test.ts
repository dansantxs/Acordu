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
    tentarDeNovo,
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

    it('sem rede na primeira carga fica indisponível (não anônima) e só tenta de novo quando pedem', async () => {
        const renovador = vi
            .fn()
            .mockRejectedValueOnce(new TypeError('Failed to fetch'))
            .mockResolvedValueOnce({ accessToken: 'a6', expiraEm: daquiA(3_600_000) })
        definirRenovador(renovador)
        await iniciarSessao()
        expect(estadoSessao().situacao).toBe('indisponivel')
        expect(renovador).toHaveBeenCalledTimes(1)

        expect((await tentarDeNovo()).situacao).toBe('ativa')
        expect(tokenAtual()).toBe('a6')
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

    describe('#222: abrir sem sessão não é encerramento', () => {
        const avisos = () => vi.spyOn(BroadcastChannel.prototype, 'postMessage')

        it('IniciarSessao_RefreshRecusado_FicaAnonimaComMotivoInicioSemAvisarAsOutrasAbas', async () => {
            const postagens = avisos()
            definirRenovador(async () => null)
            await iniciarSessao()
            expect(estadoSessao()).toEqual({ situacao: 'anonima', motivo: 'inicio' })
            expect(postagens).not.toHaveBeenCalled()
        })

        it('TentarDeNovo_DepoisDeIndisponivelERefreshRecusado_FicaAnonimaComMotivoInicio', async () => {
            definirRenovador(
                vi.fn().mockRejectedValueOnce(new TypeError('Failed to fetch')).mockResolvedValueOnce(null),
            )
            await iniciarSessao()
            expect(await tentarDeNovo()).toEqual({ situacao: 'anonima', motivo: 'inicio' })
        })

        it('Renovar_SessaoAtivaRecusada_EncerraComMotivoEncerramentoEAvisaAsOutrasAbas', async () => {
            definirRenovador(async () => ({ accessToken: 'a7', expiraEm: daquiA(3_600_000) }))
            await iniciarSessao()
            const postagens = avisos()
            definirRenovador(async () => null)
            expect(await renovar()).toBe(false)
            expect(estadoSessao()).toEqual({ situacao: 'anonima', motivo: 'encerramento' })
            expect(tokenAtual()).toBeNull()
            expect(postagens).toHaveBeenCalledWith({ tipo: 'encerrada' })
        })

        it('Canal_EncerradaEmAbaQueAindaCarrega_FicaAnonimaComMotivoInicio', async () => {
            const outraAba = new BroadcastChannel('acordu-sessao')
            try {
                outraAba.postMessage({ tipo: 'encerrada' })
                await vi.waitFor(() => expect(estadoSessao()).toEqual({ situacao: 'anonima', motivo: 'inicio' }))
            } finally {
                outraAba.close()
            }
        })

        it('Canal_EncerradaEmAbaComSessaoAtiva_EncerraComMotivoEncerramento', async () => {
            abrirSessao({ accessToken: 'a8', expiraEm: daquiA(3_600_000) })
            const outraAba = new BroadcastChannel('acordu-sessao')
            try {
                outraAba.postMessage({ tipo: 'encerrada' })
                await vi.waitFor(() => expect(estadoSessao()).toEqual({ situacao: 'anonima', motivo: 'encerramento' }))
                expect(tokenAtual()).toBeNull()
            } finally {
                outraAba.close()
            }
        })
    })

    it('trata como vencido o token a menos de um minuto do fim', () => {
        abrirSessao({ accessToken: 'a4', expiraEm: daquiA(30_000) })
        expect(tokenVencendo()).toBe(true)
        abrirSessao({ accessToken: 'a5', expiraEm: daquiA(10 * 60_000) })
        expect(tokenVencendo()).toBe(false)
    })
})
