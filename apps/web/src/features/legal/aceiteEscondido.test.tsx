import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comSessao, json, perfil, semSessao, servidor } from '@/testes/app'

// Build sem os textos legais publicados (o padrão até a #194), independente do vite.config.ts e do .env.local.
vi.mock('@/shared/config/ambiente', async (original) => ({
    ...(await original<typeof import('@/shared/config/ambiente')>()),
    TEXTOS_LEGAIS_PUBLICADOS: false,
}))

const semAvisos = () => json({ itens: [], pagina: 1, tamanhoPagina: 10, total: 0, naoLidas: 0 })

/** Aceite dos textos legais com os textos não publicados (spec do F14, F14-114, F14-117 e F14-118; D16). */
describe('aceite dos textos legais, não publicados', () => {
    beforeEach(() => {
        reiniciarParaTestes()
        sessionStorage.clear()
    })
    afterEach(() => vi.unstubAllGlobals())

    it('Cadastro_NaoPublicados_SemCaixaNemLinksEEnviaSemAceite (F14-114)', async () => {
        const chamadas = servidor({
            '/api/v1/auth/refresh': semSessao,
            '/api/v1/auth/registro': () => comSessao(),
            '/api/v1/auth/eu': () => json(perfil),
            '/api/v1/notificacoes': semAvisos,
        })
        await abrir('/cadastro')
        const usuario = userEvent.setup()
        await usuario.type(await screen.findByLabelText('Seu nome'), 'Ana')
        await usuario.type(screen.getByLabelText('E-mail'), 'ana@exemplo.com')
        await usuario.type(screen.getByLabelText('Senha'), 'umasenhaboa')
        await usuario.type(screen.getByLabelText('Nome da empresa'), 'Prado')
        await usuario.type(screen.getByLabelText('CPF ou CNPJ'), '52998224725')

        expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
        expect(screen.queryByRole('link', { name: /termos de uso|política de privacidade/i })).not.toBeInTheDocument()

        await usuario.click(screen.getByRole('button', { name: 'Criar conta' }))
        await waitFor(() => expect(chamadas.some((c) => c.caminho === '/api/v1/auth/registro')).toBe(true))
        const corpo = chamadas.find((c) => c.caminho === '/api/v1/auth/registro')?.corpo
        expect(corpo).not.toHaveProperty('versaoTermos')
        expect(corpo).not.toHaveProperty('versaoPrivacidade')
    })

    it.each([
        ['ContaNova', false],
        ['ContaExistente', true],
    ])('AceiteConviteEquipe_NaoPublicados_%s_SemCaixaNemLinksEEntraSemAceite (F14-117)', async (_, temConta) => {
        const chamadas = servidor({
            '/api/v1/auth/refresh': semSessao,
            '/api/v1/auth/convite/consulta': () =>
                json({ nomeTenant: 'Prado Consultoria', email: 'bia@exemplo.com', papel: 'Membro', temConta }),
            '/api/v1/auth/convite': () => comSessao(),
            '/api/v1/auth/eu': () => json(perfil),
            '/api/v1/notificacoes': semAvisos,
        })
        await abrir('/convite/tok-convite')
        const usuario = userEvent.setup()
        if (temConta) {
            await usuario.type(await screen.findByLabelText('Senha da sua conta'), 'umasenhaboa')
        } else {
            await usuario.type(await screen.findByLabelText('Seu nome'), 'Bia')
            await usuario.type(screen.getByLabelText('Crie uma senha'), 'umasenhaboa')
        }
        expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
        expect(screen.queryByRole('link', { name: /termos de uso|política de privacidade/i })).not.toBeInTheDocument()

        await usuario.click(screen.getByRole('button', { name: 'Entrar na equipe' }))
        await waitFor(() => expect(chamadas.some((c) => c.caminho === '/api/v1/auth/convite')).toBe(true))
        expect(chamadas.find((c) => c.caminho === '/api/v1/auth/convite')?.corpo).not.toHaveProperty('versaoTermos')
    })

    it('Casca_NaoPublicadosComPendentesNaSessao_SemAviso (F14-118)', async () => {
        // Divergência (SPA sem os textos, API com pendentes): o aviso fica escondido, sem link para "não encontrada".
        servidor({
            '/api/v1/auth/refresh': comSessao,
            '/api/v1/auth/eu': () =>
                json({ ...perfil, permissoes: ['cliente.ver'], textosPendentes: [{ texto: 'TERMOS', versao: '1' }] }),
            '/api/v1/notificacoes': semAvisos,
            'GET /api/v1/clientes': () => json({ itens: [], pagina: 1, tamanhoPagina: 50, total: 0 }),
            'GET /api/v1/equipe': () => json([]),
            'GET /api/v1/clientes/etiquetas': () => json([]),
        })
        await abrir('/clientes')
        expect(await screen.findByRole('heading', { level: 1, name: 'Clientes' })).toBeInTheDocument()
        expect(screen.queryByRole('status', { name: 'Textos legais' })).not.toBeInTheDocument()
        expect(screen.queryByRole('button', { name: 'Ciente' })).not.toBeInTheDocument()
    })
})
