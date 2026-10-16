import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, json } from '@/testes/app'

const VER = ['cliente.ver']
const GERENCIAR = ['cliente.ver', 'cliente.gerenciar']
const pagina = (itens: unknown[]) => json({ itens, pagina: 1, tamanhoPagina: 50, total: itens.length })

const padaria = {
    id: 'c1',
    nome: 'Padaria Bom Grão',
    documento: '12.345.678/0001-90',
    observacoes: 'Atende de manhã.',
    ativo: true,
    versao: '42',
    contatos: [
        {
            id: 'k1',
            nome: 'Marta Souza',
            email: 'marta@bomgrao.com',
            telefone: null,
            cargo: 'Gerente',
            ativo: true,
            temConta: true,
            acessoPortal: { situacao: 'ATIVO', escopoPortal: 'TODAS_DO_CLIENTE', conviteExpiraEm: null },
        },
    ],
}

describe('clientes', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('sem clientes, convida a cadastrar o primeiro (só quem pode gerenciar)', async () => {
        comPerfil(GERENCIAR, { 'GET /api/v1/clientes': () => pagina([]) })
        await abrir('/clientes')
        expect(await screen.findByRole('button', { name: 'Cadastrar o primeiro cliente' })).toBeInTheDocument()
    })

    it('quem só vê não tem botão de criar', async () => {
        comPerfil(VER, { 'GET /api/v1/clientes': () => pagina([]) })
        await abrir('/clientes')
        expect(await screen.findByText('Nenhum cliente ainda')).toBeInTheDocument()
        expect(screen.queryByRole('button', { name: /Novo cliente|Cadastrar/ })).not.toBeInTheDocument()
    })

    it('a busca vai para o endereço e para a API; por padrão só ativos', async () => {
        const chamadas = comPerfil(VER, {
            'GET /api/v1/clientes': (_, url) =>
                pagina(
                    url.searchParams.get('busca')
                        ? [{ id: 'c1', nome: 'Padaria Bom Grão', documento: null, ativo: true }]
                        : [],
                ),
        })
        const roteador = await abrir('/clientes')
        await screen.findByText('Nenhum cliente ainda')
        await userEvent.setup().type(screen.getByRole('searchbox', { name: 'Buscar clientes' }), 'padaria')
        expect(await screen.findByRole('link', { name: 'Padaria Bom Grão' })).toBeInTheDocument()
        expect(roteador.state.location.search).toEqual({ busca: 'padaria' })
        const ultima = chamadas.filter((c) => c.caminho === '/api/v1/clientes').at(-1)
        expect(ultima?.consulta.get('busca')).toBe('padaria')
        expect(ultima?.consulta.get('ativo')).toBe('true')
    })

    it('cria o cliente e abre a ficha', async () => {
        const chamadas = comPerfil(GERENCIAR, {
            'GET /api/v1/clientes': () => pagina([]),
            'POST /api/v1/clientes': () => json({ ...padaria, contatos: [] }, 201),
            'GET /api/v1/clientes/c1': () => json(padaria),
        })
        const roteador = await abrir('/clientes')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('button', { name: 'Novo cliente' }))
        const janela = await screen.findByRole('dialog', { name: 'Novo cliente' })
        await usuario.type(within(janela).getByLabelText('Nome'), 'Padaria Bom Grão')
        await usuario.click(within(janela).getByRole('button', { name: 'Criar cliente' }))
        expect(await screen.findByRole('heading', { level: 1, name: 'Padaria Bom Grão' })).toBeInTheDocument()
        expect(roteador.state.location.pathname).toBe('/clientes/c1')
        expect(chamadas.find((c) => c.metodo === 'POST' && c.caminho === '/api/v1/clientes')?.corpo).toEqual({
            nome: 'Padaria Bom Grão',
            documento: null,
            observacoes: null,
        })
    })

    it('a ficha mostra os contatos e adiciona um novo', async () => {
        const chamadas = comPerfil(GERENCIAR, {
            'GET /api/v1/clientes/c1': () => json(padaria),
            'POST /api/v1/clientes/c1/contatos': () => json({}, 201),
        })
        await abrir('/clientes/c1')
        expect(await screen.findByText('Marta Souza')).toBeInTheDocument()
        expect(screen.getByText('acessa o portal · todas do cliente')).toBeInTheDocument()

        const usuario = userEvent.setup()
        await usuario.click(screen.getByRole('button', { name: 'Novo contato' }))
        const janela = await screen.findByRole('dialog', { name: 'Novo contato' })
        await usuario.type(within(janela).getByLabelText('Nome'), 'João')
        await usuario.type(within(janela).getByLabelText(/E-mail/), 'joao@bomgrao.com')
        await usuario.click(within(janela).getByRole('button', { name: 'Adicionar contato' }))
        await vi.waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
        expect(chamadas.find((c) => c.caminho === '/api/v1/clientes/c1/contatos')?.corpo).toEqual({
            nome: 'João',
            email: 'joao@bomgrao.com',
            telefone: null,
            cargo: null,
        })
    })

    it('inativar pede confirmação e manda a versão', async () => {
        const chamadas = comPerfil(GERENCIAR, {
            'GET /api/v1/clientes/c1': () => json(padaria),
            'DELETE /api/v1/clientes/c1': () => new Response(null, { status: 204 }),
            'GET /api/v1/clientes': () => pagina([]),
        })
        const roteador = await abrir('/clientes/c1')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('button', { name: 'Inativar' }))
        const janela = await screen.findByRole('dialog', { name: 'Inativar Padaria Bom Grão?' })
        expect(within(janela).getByText(/perdem o acesso ao portal/)).toBeInTheDocument()
        await usuario.click(within(janela).getByRole('button', { name: 'Inativar cliente' }))
        await vi.waitFor(() => expect(roteador.state.location.pathname).toBe('/clientes'))
        expect(chamadas.find((c) => c.metodo === 'DELETE')?.ifMatch).toBe('"42"')
        expect(await screen.findByText('Padaria Bom Grão foi inativado.')).toBeInTheDocument()
    })

    it('cliente que não existe mostra "Não encontramos isso"', async () => {
        comPerfil(VER, { 'GET /api/v1/clientes/x': () => json({ status: 404, detail: 'Não encontrado.' }, 404) })
        await abrir('/clientes/x')
        expect(await screen.findByText('Não encontramos isso')).toBeInTheDocument()
    })
})
