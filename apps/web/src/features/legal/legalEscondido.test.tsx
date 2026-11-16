import { screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { guardarTokenDeLink } from '@/shared/sessao/tokenDaUrl'
import { abrir, comContato, json, servidor } from '@/testes/app'

// Build sem os textos legais publicados (o padrão até a #194), independente do vite.config.ts e do .env.local.
vi.mock('@/shared/config/ambiente', async (original) => ({
    ...(await original<typeof import('@/shared/config/ambiente')>()),
    TEXTOS_LEGAIS_PUBLICADOS: false,
}))

const semSessao = () => json({ status: 401 }, 401)

describe('textos legais escondidos (F14f, #194)', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => {
        vi.unstubAllGlobals()
        sessionStorage.clear()
    })

    it.each(['/termos', '/privacidade'])('PaginaLegal_NaoPublicada_MostraNaoEncontrada (%s)', async (caminho) => {
        servidor({ '/api/v1/auth/refresh': semSessao })
        const roteador = await abrir(caminho)
        expect(await screen.findByText('Página não encontrada.')).toBeInTheDocument()
        expect(roteador.state.location.pathname).toBe(caminho)
        expect(screen.queryByText(/em vigor desde/)).not.toBeInTheDocument()
        expect(screen.queryByRole('heading', { name: /Termos de uso|Política de privacidade/ })).not.toBeInTheDocument()
    })

    it('TelaEntrar_NaoPublicados_SemLinksLegais', async () => {
        servidor({ '/api/v1/auth/refresh': semSessao })
        await abrir('/entrar')
        expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument()
        expect(screen.queryByRole('navigation', { name: 'Textos legais' })).not.toBeInTheDocument()
        expect(screen.queryByRole('link', { name: /Política de privacidade|Termos de uso/ })).not.toBeInTheDocument()
    })

    it('AceiteConvitePortal_NaoPublicados_SemLinksLegais', async () => {
        servidor({
            '/api/v1/auth/refresh': semSessao,
            'POST /api/v1/auth/portal/convite/consulta': () =>
                json({
                    nomePrestador: 'Prado Consultoria',
                    nomeContato: 'Marta Souza',
                    email: 'marta@bomgrao.com',
                    temConta: false,
                }),
        })
        guardarTokenDeLink('convite-portal', 'tok123')
        await abrir('/portal/convite')
        expect(await screen.findByRole('textbox', { name: /Seu nome/ })).toBeInTheDocument()
        expect(screen.queryByRole('navigation', { name: 'Textos legais' })).not.toBeInTheDocument()
    })

    it('Portal_NaoPublicados_SemLinksLegais', async () => {
        comContato({
            '/api/v1/portal/solicitacoes': () => json({ itens: [], pagina: 1, tamanhoPagina: 20, total: 0 }),
        })
        await abrir('/portal')
        // O pé da casca do portal, onde ficaria o link da política.
        expect(await screen.findByText(/atendimento de .*, pelo Acordu/)).toBeInTheDocument()
        expect(screen.queryByRole('navigation', { name: 'Textos legais' })).not.toBeInTheDocument()
    })

    it('PortalPorLink_NaoPublicados_SemLinksLegais', async () => {
        servidor({ '/api/v1/auth/refresh': semSessao })
        await abrir('/portal/acompanhar')
        expect(await screen.findByText(/Entre com e-mail e senha/)).toBeInTheDocument()
        expect(screen.queryByRole('navigation', { name: 'Textos legais' })).not.toBeInTheDocument()
    })
})
