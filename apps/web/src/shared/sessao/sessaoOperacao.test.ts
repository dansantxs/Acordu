import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
    abrirSessaoOperacao,
    assinarSessaoOperacao,
    encerrarSessaoOperacao,
    motivoSemOperacao,
    reiniciarSessaoOperacaoParaTestes,
    sessaoOperacao,
} from './sessaoOperacao'

// Sessão de operação da área da Nebrys (ADR 0014, item 2): só memória, sem renovação.
describe('sessaoOperacao', () => {
    beforeEach(() => {
        vi.useFakeTimers()
        vi.setSystemTime(new Date('2026-11-21T12:00:00Z'))
        reiniciarSessaoOperacaoParaTestes()
    })
    afterEach(() => vi.useRealTimers())

    it('Abrir_DentroDoPrazo_DevolveASessao', () => {
        abrirSessaoOperacao({ accessToken: 't', expiraEm: '2026-11-21T12:30:00Z' })
        expect(sessaoOperacao()?.accessToken).toBe('t')
    })

    it('Prazo_Acabou_EncerraSozinhaComMotivoExpiradaEAvisa', () => {
        const ouvinte = vi.fn()
        abrirSessaoOperacao({ accessToken: 't', expiraEm: '2026-11-21T12:30:00Z' })
        assinarSessaoOperacao(ouvinte)
        vi.advanceTimersByTime(30 * 60_000)
        expect(sessaoOperacao()).toBeNull()
        expect(motivoSemOperacao()).toBe('expirada')
        expect(ouvinte).toHaveBeenCalled()
    })

    it('Ler_TokenJaVencido_EncerraNaLeitura', () => {
        abrirSessaoOperacao({ accessToken: 't', expiraEm: '2026-11-21T11:59:00Z' })
        expect(sessaoOperacao()).toBeNull()
        expect(motivoSemOperacao()).toBe('expirada')
    })

    it('Sair_EncerraComMotivoSaiu', () => {
        abrirSessaoOperacao({ accessToken: 't', expiraEm: '2026-11-21T12:30:00Z' })
        encerrarSessaoOperacao('saiu')
        expect(sessaoOperacao()).toBeNull()
        expect(motivoSemOperacao()).toBe('saiu')
    })

    it('Abrir_NaoGravaNadaNoNavegador', () => {
        localStorage.clear()
        sessionStorage.clear()
        abrirSessaoOperacao({ accessToken: 't', expiraEm: '2026-11-21T12:30:00Z' })
        expect(localStorage.length).toBe(0)
        expect(sessionStorage.length).toBe(0)
        expect(document.cookie).toBe('')
    })
})
