import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, json, semSessao, servidor } from '@/testes/app'
import type { ApiTurnstile } from './DesafioTurnstile'

// Build com a chave do site: a lista de espera usa o desafio com a ação dela (contrato §16; F14-54 no front).
vi.mock('@/shared/config/ambiente', async (original) => ({
    ...(await original<typeof import('@/shared/config/ambiente')>()),
    CHAVE_TURNSTILE: 'chave-do-site',
}))

function turnstileFalso() {
    const falso = {
        render: vi.fn((_: HTMLElement, opcoes: Parameters<ApiTurnstile['render']>[1]) => {
            opcoes.callback('token-da-lista')
            return 'w1'
        }),
        reset: vi.fn(),
        remove: vi.fn(),
    }
    vi.stubGlobal('turnstile', falso)
    return falso
}

async function preencher() {
    const usuario = userEvent.setup()
    await usuario.type(await screen.findByLabelText('Seu nome'), 'Ana Souza')
    await usuario.type(screen.getByLabelText('E-mail'), 'ana@estudiotraco.com.br')
    await usuario.type(screen.getByLabelText('Nome da empresa'), 'Estúdio Traço')
    return usuario
}

describe('lista de espera com Turnstile', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('ListaEspera_ComChave_DesafioNaAcaoDaListaEMandaOToken', async () => {
        const turnstile = turnstileFalso()
        const chamadas = servidor({
            '/api/v1/auth/refresh': semSessao,
            'GET /api/v1/auth/cadastro': () => json({ modo: 'CONVITE' }),
            'POST /api/v1/lista-espera': () => new Response(null, { status: 202 }),
        })
        await abrir('/cadastro')
        const usuario = await preencher()
        await waitFor(() => expect(turnstile.render).toHaveBeenCalled())
        expect(turnstile.render.mock.calls[0]![1]).toMatchObject({ sitekey: 'chave-do-site', action: 'lista-espera' })

        await usuario.click(screen.getByRole('button', { name: 'Entrar na lista de espera' }))
        await screen.findByText(/Recebemos seus dados/)
        expect(chamadas.find((c) => c.caminho === '/api/v1/lista-espera')?.corpo).toMatchObject({
            tokenTurnstile: 'token-da-lista',
        })
    })

    it('ListaEspera_SemToken_BotaoNaoEnvia', async () => {
        vi.stubGlobal('turnstile', { render: vi.fn(() => 'w1'), reset: vi.fn(), remove: vi.fn() })
        servidor({ '/api/v1/auth/refresh': semSessao, 'GET /api/v1/auth/cadastro': () => json({ modo: 'CONVITE' }) })
        await abrir('/cadastro')
        await preencher()
        const botao = screen.getByRole('button', { name: 'Entrar na lista de espera' })
        expect(botao).toBeDisabled()
        expect(botao).toHaveAccessibleDescription('Conclua a verificação acima para enviar.')
    })

    it('ListaEspera_TokenRecusado_MostraAMensagemERenovaODesafio', async () => {
        const turnstile = turnstileFalso()
        servidor({
            '/api/v1/auth/refresh': semSessao,
            'GET /api/v1/auth/cadastro': () => json({ modo: 'CONVITE' }),
            'POST /api/v1/lista-espera': () =>
                json(
                    {
                        type: 'https://acordu.app.br/erros/validacao',
                        status: 400,
                        errors: { tokenTurnstile: ['Confirme que você não é um robô e tente de novo.'] },
                    },
                    400,
                ),
        })
        await abrir('/cadastro')
        const usuario = await preencher()
        await waitFor(() => expect(turnstile.render).toHaveBeenCalled())
        await usuario.click(screen.getByRole('button', { name: 'Entrar na lista de espera' }))

        expect(await screen.findByText('Confirme que você não é um robô e tente de novo.')).toBeInTheDocument()
        expect(turnstile.reset).toHaveBeenCalledWith('w1')
    })

    it('CadastroAberto_ComChave_DesafioSegueNaAcaoDoRegistro', async () => {
        const turnstile = turnstileFalso()
        servidor({ '/api/v1/auth/refresh': semSessao, 'GET /api/v1/auth/cadastro': () => json({ modo: 'ABERTO' }) })
        await abrir('/cadastro')
        await screen.findByLabelText('Senha')
        await waitFor(() => expect(turnstile.render).toHaveBeenCalled())
        expect(turnstile.render.mock.calls[0]![1]).toMatchObject({ action: 'registro' })
    })
})
