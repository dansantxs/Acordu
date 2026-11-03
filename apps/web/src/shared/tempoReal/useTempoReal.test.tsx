import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { useTempoReal } from './useTempoReal'

const conectar = vi.hoisted(() => vi.fn(() => () => undefined))
vi.mock('./conexao', () => ({ conectarTempoReal: conectar }))

function Casca({ modo }: { modo: 'sse' | 'polling' }): ReactNode {
    useTempoReal('t1', modo)
    return null
}

describe('useTempoReal', () => {
    afterEach(() => conectar.mockClear())

    it('F8-19: no modo polling não abre a conexão SSE', () => {
        render(
            <QueryClientProvider client={new QueryClient()}>
                <Casca modo="polling" />
            </QueryClientProvider>,
        )
        expect(conectar).not.toHaveBeenCalled()
    })

    it('no modo SSE (padrão do desenvolvimento) conecta', () => {
        render(
            <QueryClientProvider client={new QueryClient()}>
                <Casca modo="sse" />
            </QueryClientProvider>,
        )
        expect(conectar).toHaveBeenCalledTimes(1)
    })
})
