import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, daquiA, json, perfil, servidor } from '@/testes/app'

// Build sem a chave do Turnstile, independente do .env.local de quem roda (F9-40).
vi.mock('@/shared/config/ambiente', async (original) => ({
    ...(await original<typeof import('@/shared/config/ambiente')>()),
    CHAVE_TURNSTILE: undefined,
}))

describe('acesso', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('sem sessão, o app manda para o login lembrando o destino', async () => {
        servidor({ '/api/v1/auth/refresh': () => new Response(null, { status: 401 }) })
        const roteador = await abrir('/?aba=1')
        expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument()
        expect(roteador.state.location.search).toEqual({ voltar: '/?aba=1' })
    })

    it('com o cookie válido, abre o app com o perfil', async () => {
        servidor({
            '/api/v1/auth/refresh': () => json({ accessToken: 'a', expiraEm: daquiA(3_600_000) }),
            '/api/v1/auth/eu': () => json(perfil),
        })
        await abrir('/')
        expect(await screen.findByRole('heading', { level: 1, name: /, Ana$/ })).toBeInTheDocument()
        expect(screen.getByText('Prado Consultoria')).toBeInTheDocument()
    })

    it('mostra a recusa do login e entra quando a senha confere', async () => {
        let tentativas = 0
        servidor({
            '/api/v1/auth/refresh': () => new Response(null, { status: 401 }),
            '/api/v1/auth/login': () =>
                ++tentativas === 1
                    ? json(
                          { status: 401, detail: 'E-mail ou senha inválidos, ou acesso bloqueado temporariamente.' },
                          401,
                      )
                    : json({ accessToken: 'a', expiraEm: daquiA(3_600_000) }),
            '/api/v1/auth/eu': () => json(perfil),
        })
        await abrir('/entrar')
        const usuario = userEvent.setup()
        await usuario.type(await screen.findByLabelText('E-mail'), 'ana@exemplo.com')
        await usuario.type(screen.getByLabelText('Senha'), 'senha errada')
        await usuario.click(screen.getByRole('button', { name: 'Entrar' }))
        expect(await screen.findByRole('alert')).toHaveTextContent('E-mail ou senha inválidos')

        await usuario.click(screen.getByRole('button', { name: 'Entrar' }))
        expect(await screen.findByRole('heading', { level: 1, name: /, Ana$/ })).toBeInTheDocument()
    })

    it('mostra no campo o erro de validação do cadastro', async () => {
        servidor({
            '/api/v1/auth/refresh': () => new Response(null, { status: 401 }),
            '/api/v1/auth/registro': () =>
                json(
                    {
                        type: 'https://acordu.app.br/erros/validacao',
                        status: 400,
                        errors: { email: ['Este e-mail já tem conta.'] },
                    },
                    400,
                ),
        })
        await abrir('/cadastro')
        const usuario = userEvent.setup()
        await usuario.type(await screen.findByLabelText('Seu nome'), 'Ana')
        await usuario.type(screen.getByLabelText('E-mail'), 'ana@exemplo.com')
        await usuario.type(screen.getByLabelText('Senha'), 'umasenhaboa')
        await usuario.type(screen.getByLabelText('Nome da empresa'), 'Prado')
        await usuario.click(screen.getByRole('button', { name: 'Criar conta' }))
        expect(await screen.findByText('Este e-mail já tem conta.')).toBeInTheDocument()
        expect(screen.getByLabelText('E-mail')).toHaveAttribute('aria-invalid', 'true')
    })

    it('contato de cliente vai para o portal', async () => {
        servidor({
            '/api/v1/auth/refresh': () => json({ accessToken: 'a', expiraEm: daquiA(3_600_000) }),
            '/api/v1/auth/eu': () => json({ ...perfil, papel: 'Cliente', clienteId: 'c1', escopoPortal: 'PROPRIAS' }),
            '/api/v1/portal/eu': () =>
                json({
                    contato: { id: 'k1', nome: 'Marta', email: 'm@x.com' },
                    cliente: { id: 'c1', nome: 'Padaria' },
                    prestador: { nome: 'Prado', logoUrl: null },
                    escopoPortal: 'PROPRIAS',
                    veContratos: false,
                    envioDisponivel: true,
                    solicitacaoId: null,
                }),
            '/api/v1/portal/solicitacoes': () => json({ itens: [], pagina: 1, tamanhoPagina: 20, total: 0 }),
        })
        const roteador = await abrir('/')
        expect(await screen.findByRole('heading', { name: 'Solicitações' })).toBeInTheDocument()
        expect(roteador.state.location.pathname).toBe('/portal')
    })
    it('se a sessão não pôde ser conferida (429), oferece tentar de novo em vez do login', async () => {
        let tentativas = 0
        servidor({
            '/api/v1/auth/refresh': () =>
                ++tentativas === 1
                    ? json({ status: 429 }, 429)
                    : json({ accessToken: 'a', expiraEm: daquiA(3_600_000) }),
            '/api/v1/auth/eu': () => json(perfil),
            '/api/v1/notificacoes': () => json({ itens: [], pagina: 1, tamanhoPagina: 10, total: 0, naoLidas: 0 }),
        })
        await abrir('/')
        expect(await screen.findByRole('heading', { name: 'Não deu para conectar' })).toBeInTheDocument()
        expect(screen.queryByRole('heading', { name: 'Entrar' })).not.toBeInTheDocument()

        await userEvent.setup().click(screen.getByRole('button', { name: 'Tentar de novo' }))
        expect(await screen.findByRole('heading', { level: 1, name: /, Ana$/ })).toBeInTheDocument()
    })

    it('sem a chave do Turnstile, o cadastro não mostra desafio nem manda token (F9-40)', async () => {
        const turnstile = { render: vi.fn(() => 'w1'), reset: vi.fn(), remove: vi.fn() }
        vi.stubGlobal('turnstile', turnstile)
        const chamadas = servidor({
            '/api/v1/auth/refresh': () => new Response(null, { status: 401 }),
            '/api/v1/auth/registro': () =>
                json({ type: 'https://acordu.app.br/erros/validacao', status: 400, errors: { email: ['x'] } }, 400),
        })
        await abrir('/cadastro')
        const usuario = userEvent.setup()
        await usuario.type(await screen.findByLabelText('Seu nome'), 'Ana')
        await usuario.type(screen.getByLabelText('E-mail'), 'ana@exemplo.com')
        await usuario.type(screen.getByLabelText('Senha'), 'umasenhaboa')
        await usuario.type(screen.getByLabelText('Nome da empresa'), 'Prado')
        await usuario.click(screen.getByRole('button', { name: 'Criar conta' }))
        await screen.findByText('x')
        expect(turnstile.render).not.toHaveBeenCalled()
        expect(chamadas.find((c) => c.caminho === '/api/v1/auth/registro')?.corpo).not.toHaveProperty('tokenTurnstile')
    })
})
