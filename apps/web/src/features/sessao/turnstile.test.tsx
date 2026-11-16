import { act, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, daquiA, json, servidor } from '@/testes/app'
import type { ApiTurnstile } from './DesafioTurnstile'

// Build com a chave do site (VITE_TURNSTILE_CHAVE): o cadastro mostra o desafio (spec do F9, F9-39 a F9-41).
vi.mock('@/shared/config/ambiente', async (original) => ({
    ...(await original<typeof import('@/shared/config/ambiente')>()),
    CHAVE_TURNSTILE: 'chave-do-site',
}))

/** O widget do Cloudflare, falso: entrega o token na hora em que é desenhado. */
function turnstileFalso(token = 'token-do-desafio') {
    const falso = {
        render: vi.fn((_: HTMLElement, opcoes: Parameters<ApiTurnstile['render']>[1]) => {
            opcoes.callback(token)
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
    await usuario.type(await screen.findByLabelText('Seu nome'), 'Ana')
    await usuario.type(screen.getByLabelText('E-mail'), 'ana@exemplo.com')
    await usuario.type(screen.getByLabelText('Senha'), 'umasenhaboa')
    await usuario.type(screen.getByLabelText('Nome da empresa'), 'Prado')
    await usuario.type(screen.getByLabelText('CPF ou CNPJ'), '52998224725')
    return usuario
}

describe('cadastro com Turnstile', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('desenha o desafio com a chave e manda o token no cadastro (F9-39)', async () => {
        const turnstile = turnstileFalso()
        const chamadas = servidor({
            '/api/v1/auth/refresh': () => new Response(null, { status: 401 }),
            '/api/v1/auth/registro': () => json({ accessToken: 'a', expiraEm: daquiA(3_600_000) }, 201),
            '/api/v1/auth/eu': () => new Response(null, { status: 401 }),
        })
        await abrir('/cadastro')
        const usuario = await preencher()
        await waitFor(() => expect(turnstile.render).toHaveBeenCalled())
        expect(turnstile.render.mock.calls[0]![1]).toMatchObject({ sitekey: 'chave-do-site', action: 'registro' })

        await usuario.click(screen.getByRole('button', { name: 'Criar conta' }))
        await waitFor(() => expect(chamadas.some((c) => c.caminho === '/api/v1/auth/registro')).toBe(true))
        expect(chamadas.find((c) => c.caminho === '/api/v1/auth/registro')?.corpo).toMatchObject({
            tokenTurnstile: 'token-do-desafio',
        })
    })

    it('sem o token, o botão não envia (F9-39)', async () => {
        vi.stubGlobal('turnstile', { render: vi.fn(() => 'w1'), reset: vi.fn(), remove: vi.fn() })
        servidor({ '/api/v1/auth/refresh': () => new Response(null, { status: 401 }) })
        await abrir('/cadastro')
        await preencher()
        expect(screen.getByRole('button', { name: 'Criar conta' })).toBeDisabled()
    })

    it('token recusado mostra a mensagem e renova o desafio (F9-41)', async () => {
        const turnstile = turnstileFalso()
        servidor({
            '/api/v1/auth/refresh': () => new Response(null, { status: 401 }),
            '/api/v1/auth/registro': () =>
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
        await usuario.click(screen.getByRole('button', { name: 'Criar conta' }))

        expect(await screen.findByText('Confirme que você não é um robô e tente de novo.')).toBeInTheDocument()
        expect(turnstile.reset).toHaveBeenCalledWith('w1')
    })

    it('limite de cadastros explica que é por hora e renova o desafio (F9-41)', async () => {
        const turnstile = turnstileFalso()
        servidor({
            '/api/v1/auth/refresh': () => new Response(null, { status: 401 }),
            '/api/v1/auth/registro': () => json({ status: 429, title: 'Muitas requisições' }, 429),
        })
        await abrir('/cadastro')
        const usuario = await preencher()
        await waitFor(() => expect(turnstile.render).toHaveBeenCalled())
        await usuario.click(screen.getByRole('button', { name: 'Criar conta' }))

        expect(await screen.findByText(/Muitos cadastros a partir desta rede/)).toBeInTheDocument()
        expect(turnstile.reset).toHaveBeenCalled()
    })

    it('Enter num campo sem o token não envia o cadastro (F9-39)', async () => {
        vi.stubGlobal('turnstile', { render: vi.fn(() => 'w1'), reset: vi.fn(), remove: vi.fn() })
        const chamadas = servidor({ '/api/v1/auth/refresh': () => new Response(null, { status: 401 }) })
        await abrir('/cadastro')
        const usuario = await preencher()
        await usuario.type(screen.getByLabelText('Nome da empresa'), '{Enter}')
        expect(screen.getByRole('button', { name: 'Criar conta' })).toHaveAccessibleDescription(
            'Conclua a verificação acima para criar a conta.',
        )
        expect(chamadas.some((c) => c.caminho === '/api/v1/auth/registro')).toBe(false)
    })

    it('token vencido volta a travar o botão (F9-39)', async () => {
        let opcoes: Parameters<ApiTurnstile['render']>[1] | undefined
        vi.stubGlobal('turnstile', {
            render: vi.fn((_: HTMLElement, o: Parameters<ApiTurnstile['render']>[1]) => {
                opcoes = o
                o.callback('token-do-desafio')
                return 'w1'
            }),
            reset: vi.fn(),
            remove: vi.fn(),
        })
        servidor({ '/api/v1/auth/refresh': () => new Response(null, { status: 401 }) })
        await abrir('/cadastro')
        await preencher()
        await waitFor(() => expect(screen.getByRole('button', { name: 'Criar conta' })).toBeEnabled())
        act(() => opcoes!['expired-callback']())
        expect(screen.getByRole('button', { name: 'Criar conta' })).toBeDisabled()
    })

    it('ao sair da tela, o widget é removido (F9-39)', async () => {
        const turnstile = turnstileFalso()
        servidor({ '/api/v1/auth/refresh': () => new Response(null, { status: 401 }) })
        await abrir('/cadastro')
        await waitFor(() => expect(turnstile.render).toHaveBeenCalled())
        await userEvent.setup().click(screen.getByRole('link', { name: 'Já tenho conta' }))
        await waitFor(() => expect(turnstile.remove).toHaveBeenCalledWith('w1'))
    })

    it('script do Cloudflare que não carrega avisa na tela (F9-39)', async () => {
        servidor({ '/api/v1/auth/refresh': () => new Response(null, { status: 401 }) })
        await abrir('/cadastro')
        await screen.findByLabelText('Seu nome')
        const script = document.head.querySelector<HTMLScriptElement>('script[src*="challenges.cloudflare.com"]')
        expect(script).not.toBeNull()
        act(() => script!.dispatchEvent(new Event('error')))
        expect(await screen.findByText(/Não deu para carregar a verificação/)).toBeInTheDocument()
        expect(document.head.querySelector('script[src*="challenges.cloudflare.com"]')).toBeNull()
    })
})
