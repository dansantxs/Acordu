import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { routeTree } from '@/routeTree.gen'
import '@/shared/api/cliente'
import { iniciarSessao, reiniciarParaTestes } from '@/shared/sessao/sessao'

const daquiA = (ms: number) => new Date(Date.now() + ms).toISOString()
const json = (corpo: unknown, status = 200) =>
    new Response(JSON.stringify(corpo), { status, headers: { 'content-type': 'application/json' } })

const perfil = {
    usuarioId: 'u1',
    nome: 'Ana Prado',
    email: 'ana@exemplo.com',
    emailConfirmado: true,
    tenant: { id: 't1', nome: 'Prado Consultoria', fusoHorario: 'America/Sao_Paulo' },
    papel: 'Dono',
    permissoes: [],
    clienteId: null,
    escopoPortal: null,
    tenants: [{ id: 't1', nome: 'Prado Consultoria', papel: 'Dono' }],
}

function servidor(rotas: Record<string, () => Response>) {
    vi.stubGlobal(
        'fetch',
        vi.fn(async (requisicao: Request) => {
            const resposta = rotas[new URL(requisicao.url).pathname]
            if (!resposta) throw new Error(`Sem resposta para ${requisicao.url}`)
            return resposta()
        }),
    )
}

async function abrir(caminho: string) {
    const roteador = createRouter({
        routeTree,
        context: { consultas: new QueryClient({ defaultOptions: { queries: { retry: false } } }) },
        history: createMemoryHistory({ initialEntries: [caminho] }),
    })
    await iniciarSessao()
    render(
        <QueryClientProvider client={roteador.options.context.consultas}>
            <RouterProvider router={roteador} />
        </QueryClientProvider>,
    )
    return roteador
}

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
        expect(await screen.findByText('Olá, Ana Prado')).toBeInTheDocument()
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
        expect(await screen.findByText('Olá, Ana Prado')).toBeInTheDocument()
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
        })
        await abrir('/')
        expect(await screen.findByRole('heading', { name: 'Portal do cliente' })).toBeInTheDocument()
    })
})
