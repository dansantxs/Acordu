import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { filaDeAvisos } from '@/shared/avisos/avisos'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { comSessao, json, servidor } from '@/testes/app'
import { definirLembrete } from './lembrete'
import { useLembreteCronometro } from './useLembreteCronometro'

// Expediente o dia inteiro, todo dia: o teste não depende da hora em que roda.
const sempre = {
    fusoHorario: 'America/Sao_Paulo',
    expediente: ['DOMINGO', 'SEGUNDA', 'TERCA', 'QUARTA', 'QUINTA', 'SEXTA', 'SABADO'].map((diaSemana) => ({
        diaSemana,
        inicio: '00:00',
        fim: '23:59',
    })),
    feriados: [],
}

describe('useLembreteCronometro', () => {
    let fila: ReturnType<typeof vi.spyOn>
    beforeEach(() => {
        reiniciarParaTestes()
        fila = vi.spyOn(filaDeAvisos, 'add')
        servidor({
            '/api/v1/auth/refresh': comSessao,
            '/api/v1/configuracoes/calendario': () => json(sempre),
            '/api/v1/cronometro': () => new Response(null, { status: 204 }),
        })
        vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['setInterval', 'setTimeout', 'Date'] })
    })
    afterEach(() => {
        vi.useRealTimers()
        fila.mockRestore()
        vi.unstubAllGlobals()
        localStorage.clear()
    })

    const montar = (ativo: boolean) => {
        const consultas = new QueryClient({ defaultOptions: { queries: { retry: false } } })
        const envolver = ({ children }: { children: ReactNode }) => (
            <QueryClientProvider client={consultas}>{children}</QueryClientProvider>
        )
        return renderHook(() => useLembreteCronometro('t1:u1', ativo, vi.fn()), { wrapper: envolver })
    }
    const lembretes = () =>
        fila.mock.calls.filter(
            ([aviso]: unknown[]) => (aviso as { texto: string }).texto === 'Nenhum cronômetro rodando. Começar agora?',
        )

    it('montado, com o lembrete ligado e sem cronômetro, mostra o aviso no app (F12-13)', async () => {
        definirLembrete('t1:u1', 15, Date.now() - 60 * 60_000)
        montar(true)
        await vi.advanceTimersByTimeAsync(14 * 60_000)
        expect(lembretes()).toHaveLength(0)
        await vi.advanceTimersByTimeAsync(2 * 60_000)
        await vi.waitFor(() => expect(lembretes()).toHaveLength(1))
    })

    it('sem apontamento.criar (inativo), nada aparece', async () => {
        definirLembrete('t1:u1', 15, Date.now() - 60 * 60_000)
        montar(false)
        await vi.advanceTimersByTimeAsync(20 * 60_000)
        expect(lembretes()).toHaveLength(0)
    })
})
