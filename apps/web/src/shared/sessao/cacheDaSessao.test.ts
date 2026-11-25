import { QueryClient } from '@tanstack/react-query'
import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from 'vitest'
import { limparCacheAoTrocarSessao } from './cacheDaSessao'
import { abrirSessao, definirRenovador, encerrarSessao, iniciarSessao, reiniciarParaTestes } from './sessao'

const daquiA = (ms: number) => new Date(Date.now() + ms).toISOString()

describe('cache e sessão', () => {
    let consultas: QueryClient
    let invalidar: Mock<() => void>

    beforeEach(() => {
        reiniciarParaTestes()
        consultas = new QueryClient()
        consultas.setQueryData(['cadastro', 'convite', 'tok'], { email: 'ana@estudiotraco.com.br' })
        invalidar = vi.fn<() => void>()
        limparCacheAoTrocarSessao(consultas, invalidar)
    })
    afterEach(() => vi.restoreAllMocks())

    it('LimparCache_PrimeiraCargaSemSessao_MantemAsConsultas', async () => {
        definirRenovador(async () => null)
        await iniciarSessao()
        expect(consultas.getQueryData(['cadastro', 'convite', 'tok'])).toEqual({ email: 'ana@estudiotraco.com.br' })
        expect(invalidar).not.toHaveBeenCalled()
    })

    it('LimparCache_PrimeiraCargaComSessao_MantemAsConsultas', async () => {
        definirRenovador(async () => ({ accessToken: 'a1', expiraEm: daquiA(3_600_000) }))
        await iniciarSessao()
        expect(consultas.getQueryData(['cadastro', 'convite', 'tok'])).toBeDefined()
        expect(invalidar).not.toHaveBeenCalled()
    })

    it('LimparCache_SessaoAtivaEncerrada_LimpaEInvalidaAsRotas', () => {
        abrirSessao({ accessToken: 'a2', expiraEm: daquiA(3_600_000) })
        consultas.setQueryData(['perfil'], { nome: 'Ana' })
        invalidar.mockClear()
        encerrarSessao()
        expect(consultas.getQueryCache().getAll()).toHaveLength(0)
        expect(invalidar).toHaveBeenCalledTimes(1)
    })

    it('LimparCache_TrocaDeSessao_LimpaEInvalidaAsRotas', () => {
        abrirSessao({ accessToken: 'a3', expiraEm: daquiA(3_600_000) })
        expect(consultas.getQueryCache().getAll()).toHaveLength(0)
        expect(invalidar).toHaveBeenCalledTimes(1)
    })
})
