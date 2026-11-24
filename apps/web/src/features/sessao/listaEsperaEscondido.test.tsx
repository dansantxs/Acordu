import { screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, json, semSessao, servidor } from '@/testes/app'

// Build sem os textos legais publicados (o padrão até a #194): o link da política some, como nas outras telas (D16).
vi.mock('@/shared/config/ambiente', async (original) => ({
    ...(await original<typeof import('@/shared/config/ambiente')>()),
    TEXTOS_LEGAIS_PUBLICADOS: false,
}))

describe('lista de espera, textos não publicados', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('ListaEspera_TextosNaoPublicados_SemLinkNemAceiteEOFormularioSegue', async () => {
        servidor({ '/api/v1/auth/refresh': semSessao, 'GET /api/v1/auth/cadastro': () => json({ modo: 'CONVITE' }) })
        await abrir('/cadastro')
        await screen.findByRole('heading', { level: 1, name: 'Lista de espera' })

        expect(screen.queryByRole('link', { name: /política de privacidade|termos de uso/i })).not.toBeInTheDocument()
        expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
        expect(screen.getByRole('button', { name: 'Entrar na lista de espera' })).toBeInTheDocument()
    })
})
