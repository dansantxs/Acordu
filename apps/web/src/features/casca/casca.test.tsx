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
        expect(links).toEqual(['Hoje', 'Agenda', 'Solicitações', 'Tarefas', 'Horas trabalhadas', 'Novidades', 'Ajuda'])
        // Grupo sem nenhum destino do papel (Cadastros) não aparece.
        expect(within(capa).queryByText('Cadastros')).not.toBeInTheDocument()
        expect(within(capa).getByRole('list', { name: 'Atendimento' })).toBeInTheDocument()
    })

    it('a capa segue a ordem de uso, em grupos com título (pedido de 05/11)', async () => {
        app([
            'solicitacao.ver',
            'tarefa.ver',
            'cliente.ver',
            'contrato.ver',
            'projeto.ver',
            'recorrencia.ver',
            'dashboard.ver',
            'configuracao.gerenciar',
        ])
        await abrir('/')
        const capa = await screen.findByRole('navigation', { name: 'Principal' })
        const nomes = (lista: HTMLElement) =>
            within(lista)
                .getAllByRole('link')
                .map((l) => l.textContent?.replace(', há novidades', ''))
        expect(nomes(capa)).toEqual([
            'Hoje',
            'Agenda',
            'Clientes',
            'Contratos',
            'Solicitações',
            'Tarefas',
            'Projetos',
            'Recorrências',
            'Horas trabalhadas',
            'Panorama',
            'Configurações',
            'Novidades',
            'Ajuda',
        ])
        expect(nomes(within(capa).getByRole('list', { name: 'Cadastros' }))).toEqual(['Clientes', 'Contratos'])
        expect(nomes(within(capa).getByRole('list', { name: 'Atendimento' }))).toEqual([
            'Solicitações',
            'Tarefas',
            'Projetos',
            'Recorrências',
        ])
        expect(nomes(within(capa).getByRole('list', { name: 'Acompanhamento' }))).toEqual([
            'Horas trabalhadas',
            'Panorama',
        ])
        // O grupo do sistema vem por último, sem título (só a linha).
        const listas = within(capa).getAllByRole('list')
        expect(nomes(listas.at(-1)!)).toEqual(['Configurações', 'Novidades', 'Ajuda'])
        expect(listas.at(-1)).not.toHaveAccessibleName()
    })

    it('na gaveta do celular, os títulos dos grupos não repetem id com a capa fixa', async () => {
        app(['cliente.ver', 'contrato.ver', 'solicitacao.ver'])
        await abrir('/')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('button', { name: 'Abrir menu' }))
        const gaveta = await screen.findByRole('dialog', { name: 'Menu' })
        expect(within(gaveta).getByRole('list', { name: 'Cadastros' })).toBeInTheDocument()
        const ids = [...document.querySelectorAll('[id]')].map((e) => e.id).filter(Boolean)
        expect(new Set(ids).size).toBe(ids.length)
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

    it('com a notificação do navegador ligada, aviso novo do sino vira notificação, e o clique abre o item (F12-22, F12-23)', async () => {
        const criadas: { titulo: string; onclick: (() => void) | null; close: () => void }[] = []
        vi.stubGlobal(
            'Notification',
            class {
                static permission = 'granted'
                onclick: (() => void) | null = null
                close = vi.fn()
                constructor(public titulo: string) {
                    criadas.push(this)
                }
            },
        )
        localStorage.setItem(
            `acordu:notificacoes-navegador:${perfil.tenant.id}:${perfil.usuarioId}`,
            String(Date.now() - 60_000),
        )
        const aviso = {
            id: 'n7',
            categoria: 'ATRIBUICAO',
            titulo: 'Rafael passou a tarefa "Arte" para você',
            link: 'http://localhost:5173/tarefas/t1',
            criadaEm: new Date().toISOString(),
            lidaEm: null,
        }
        const chamadas = app([], {
            '/api/v1/notificacoes': () => json({ itens: [aviso], pagina: 1, tamanhoPagina: 10, total: 1, naoLidas: 1 }),
            '/api/v1/notificacoes/n7/leitura': () => new Response(null, { status: 204 }),
        })
        try {
            const roteador = await abrir('/')
            await vi.waitFor(() => expect(criadas).toHaveLength(1))
            expect(criadas[0]!.titulo).toBe('Rafael passou a tarefa "Arte" para você')
            const foco = vi.spyOn(window, 'focus').mockImplementation(() => undefined)
            criadas[0]!.onclick!()
            expect(foco).toHaveBeenCalled()
            foco.mockRestore()
            await vi.waitFor(() => expect(roteador.state.location.pathname).toBe('/tarefas/t1'))
            await vi.waitFor(() =>
                expect(chamadas.some((c) => c.caminho === '/api/v1/notificacoes/n7/leitura')).toBe(true),
            )
        } finally {
            localStorage.clear()
        }
    })
    it('com a aba escondida, o sino continua consultando e o aviso novo vira notificação (F12-22)', async () => {
        const criadas: string[] = []
        vi.stubGlobal(
            'Notification',
            class {
                static permission = 'granted'
                onclick = null
                close = vi.fn()
                constructor(titulo: string) {
                    criadas.push(titulo)
                }
            },
        )
        const chave = `acordu:notificacoes-navegador:${perfil.tenant.id}:${perfil.usuarioId}`
        localStorage.setItem(chave, String(Date.now() - 60_000))
        let itens: unknown[] = []
        app([], {
            '/api/v1/notificacoes': () =>
                json({ itens, pagina: 1, tamanhoPagina: 10, total: itens.length, naoLidas: itens.length }),
        })
        vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['setInterval', 'setTimeout'] })
        const visibilidade = Object.getOwnPropertyDescriptor(Document.prototype, 'visibilityState')
        try {
            await abrir('/')
            await screen.findByRole('button', { name: 'Avisos' })
            // Minimiza: a aba fica escondida e o aviso chega depois.
            Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => 'hidden' })
            document.dispatchEvent(new Event('visibilitychange'))
            itens = [
                {
                    id: 'n9',
                    categoria: 'SLA',
                    titulo: 'A solicitação #42 está em risco de SLA',
                    link: 'http://localhost:5173/solicitacoes/s1',
                    criadaEm: new Date().toISOString(),
                    lidaEm: null,
                },
            ]
            await vi.advanceTimersByTimeAsync(61_000)
            await vi.waitFor(() => expect(criadas).toEqual(['A solicitação #42 está em risco de SLA']))
        } finally {
            vi.useRealTimers()
            delete (document as { visibilityState?: unknown }).visibilityState
            if (visibilidade) Object.defineProperty(Document.prototype, 'visibilityState', visibilidade)
            localStorage.clear()
        }
    })

    it('o lembrete fica montado só para quem aponta horas (F12-13)', async () => {
        const intervalos = vi.spyOn(window, 'setInterval')
        app(['tarefa.ver'])
        await abrir('/')
        await screen.findByRole('navigation', { name: 'Principal' })
        const semPermissao = intervalos.mock.calls.filter(([, ms]) => ms === 60_000).length
        intervalos.mockClear()
        reiniciarParaTestes()
        app(['tarefa.ver', 'apontamento.criar'])
        await abrir('/')
        await screen.findByRole('navigation', { name: 'Principal' })
        expect(intervalos.mock.calls.filter(([, ms]) => ms === 60_000).length).toBeGreaterThan(semPermissao)
        intervalos.mockRestore()
    })

    it('endereço inexistente mostra a página não encontrada dentro da casca', async () => {
        app([])
        await abrir('/nao-existe')
        expect(await screen.findByText('Página não encontrada')).toBeInTheDocument()
        expect(screen.getByRole('navigation', { name: 'Principal' })).toBeInTheDocument()
    })

    it('empresa não liberada: toda tela explica o que fica parado e reenvia o link (F9-42)', async () => {
        const chamadas = app([], {
            '/api/v1/auth/eu': () =>
                json({
                    ...perfil,
                    permissoes: [],
                    emailConfirmado: false,
                    tenant: { ...perfil.tenant, envioLiberado: false },
                }),
            '/api/v1/auth/confirmacao-email/reenvio': () => new Response(null, { status: 202 }),
        })
        await abrir('/ajuda')
        const aviso = await screen.findByText(/Confirme seu e-mail para liberar convites, links do portal/)
        expect(aviso).toBeInTheDocument()
        await userEvent.setup().click(screen.getByRole('button', { name: 'Reenviar link' }))
        expect(await screen.findByText(/Enviamos um link novo/)).toBeInTheDocument()
        expect(chamadas.some((c) => c.caminho === '/api/v1/auth/confirmacao-email/reenvio')).toBe(true)
    })

    it('conta não confirmada em empresa liberada: sem o aviso de envio parado (F9-26)', async () => {
        app([], { '/api/v1/auth/eu': () => json({ ...perfil, permissoes: [], emailConfirmado: false }) })
        await abrir('/ajuda')
        await screen.findByRole('heading', { name: 'Ajuda' })
        expect(screen.queryByText(/Confirme seu e-mail para liberar/)).not.toBeInTheDocument()
    })
})
