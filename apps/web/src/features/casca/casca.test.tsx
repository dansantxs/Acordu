import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comSessao, json, perfil, servidor, type Rotas } from '@/testes/app'

const semAvisos = () => json({ itens: [], pagina: 1, tamanhoPagina: 10, total: 0, naoLidas: 0 })

function app(permissoes: string[], extras: Rotas = {}) {
    return servidor({
        '/api/v1/auth/refresh': comSessao,
        '/api/v1/auth/eu': () => json({ ...perfil, permissoes }),
        '/api/v1/notificacoes': semAvisos,
        ...extras,
    })
}

describe('casca do app', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('a capa só mostra o que o papel pode ver', async () => {
        app(['solicitacao.ver', 'tarefa.ver'])
        await abrir('/')
        const capa = await screen.findByRole('navigation', { name: 'Principal' })
        const links = within(capa)
            .getAllByRole('link')
            .map((l) => l.textContent?.replace(', há novidades', ''))
        expect(links).toEqual(['Hoje', 'Agenda', 'Solicitações', 'Tarefas', 'Horas', 'Novidades', 'Ajuda'])
    })

    it('o couro escolhido no menu vale no app e fica no navegador', async () => {
        localStorage.removeItem('acordu.couro')
        app([])
        await abrir('/')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('button', { name: /Ana Prado/ }))
        await usuario.click(screen.getByRole('menuitem', { name: /Bordô/ }))
        expect(document.documentElement.dataset.couro).toBe('bordo')
        expect(localStorage.getItem('acordu.couro')).toBe('bordo')
        await usuario.click(screen.getByRole('button', { name: /Ana Prado/ }))
        expect(within(screen.getByRole('menuitem', { name: /Bordô/ })).getByLabelText('escolhido')).toBeInTheDocument()
        await usuario.click(screen.getByRole('menuitem', { name: /Café/ }))
        expect(document.documentElement.dataset.couro).toBeUndefined()
    })

    it('marca o destino atual', async () => {
        app(['cliente.ver'])
        await abrir('/clientes')
        const capa = await screen.findByRole('navigation', { name: 'Principal' })
        expect(within(capa).getByRole('link', { name: 'Clientes' })).toHaveAttribute('data-status', 'active')
        expect(within(capa).getByRole('link', { name: 'Hoje' })).not.toHaveAttribute('data-status', 'active')
    })

    it('Ctrl+K busca solicitações e clientes e vai para o escolhido', async () => {
        const chamadas = app(['solicitacao.ver', 'cliente.ver'], {
            '/api/v1/solicitacoes': () =>
                json({
                    itens: [
                        {
                            id: 's1',
                            numero: 42,
                            titulo: 'Impressora da recepção',
                            cliente: { id: 'c1', nome: 'Padaria Bom Grão' },
                        },
                    ],
                    pagina: 1,
                    tamanhoPagina: 5,
                    total: 1,
                }),
            '/api/v1/clientes': () =>
                json({
                    itens: [{ id: 'c1', nome: 'Padaria Bom Grão', documento: null, ativo: true }],
                    pagina: 1,
                    tamanhoPagina: 5,
                    total: 1,
                }),
        })
        const roteador = await abrir('/')
        await screen.findByRole('heading', { level: 1 })
        const usuario = userEvent.setup()
        await usuario.keyboard('{Control>}k{/Control}')
        const busca = await screen.findByRole('searchbox', { name: 'Buscar' })
        await usuario.type(busca, 'bom')
        const opcao = await screen.findByRole('menuitem', { name: /#42 Impressora da recepção/ })
        expect(screen.getByRole('menuitem', { name: 'Padaria Bom Grão' })).toBeInTheDocument()
        await usuario.click(opcao)
        await vi.waitFor(() => expect(roteador.state.location.pathname).toBe('/solicitacoes/s1'))
        expect(chamadas.some((c) => c.caminho === '/api/v1/solicitacoes')).toBe(true)
    })

    it('sem permissão de cliente, a paleta não consulta clientes', async () => {
        const chamadas = app(['solicitacao.ver'], {
            '/api/v1/solicitacoes': () => json({ itens: [], pagina: 1, tamanhoPagina: 5, total: 0 }),
        })
        await abrir('/')
        await screen.findByRole('heading', { level: 1 })
        const usuario = userEvent.setup()
        await usuario.keyboard('{Control>}k{/Control}')
        await usuario.type(await screen.findByRole('searchbox', { name: 'Buscar' }), 'xyz')
        expect(await screen.findByText('Nada encontrado para “xyz”.')).toBeInTheDocument()
        expect(chamadas.some((c) => c.caminho === '/api/v1/clientes')).toBe(false)
    })

    it('o sino conta os não lidos, abre o aviso e marca como lido', async () => {
        const chamadas = app([], {
            '/api/v1/notificacoes': () =>
                json({
                    itens: [
                        {
                            id: 'n1',
                            categoria: 'MENCAO',
                            titulo: 'Rafael mencionou você na solicitação #42',
                            link: 'http://localhost:5173/solicitacoes/s1',
                            criadaEm: new Date().toISOString(),
                            lidaEm: null,
                        },
                    ],
                    pagina: 1,
                    tamanhoPagina: 10,
                    total: 1,
                    naoLidas: 1,
                }),
            '/api/v1/notificacoes/n1/leitura': () => new Response(null, { status: 204 }),
        })
        const roteador = await abrir('/')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('button', { name: 'Avisos, 1 não lidos' }))
        await usuario.click(await screen.findByRole('button', { name: /Rafael mencionou você/ }))
        await vi.waitFor(() => expect(roteador.state.location.pathname).toBe('/solicitacoes/s1'))
        expect(chamadas.some((c) => c.caminho === '/api/v1/notificacoes/n1/leitura')).toBe(true)
    })
    it('endereço inexistente mostra a página não encontrada dentro da casca', async () => {
        app([])
        await abrir('/nao-existe')
        expect(await screen.findByText('Página não encontrada')).toBeInTheDocument()
        expect(screen.getByRole('navigation', { name: 'Principal' })).toBeInTheDocument()
    })
})
