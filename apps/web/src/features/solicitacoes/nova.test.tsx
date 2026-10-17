import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, json, perfil, type Rotas } from '@/testes/app'

const PADARIA = '0199f0a0-0000-7000-8000-0000000000c1'
const RAFAEL = '0199f0a0-0000-7000-8000-000000000002'
const pagina = (itens: unknown[]) => json({ itens, pagina: 1, tamanhoPagina: 50, total: itens.length })

const padaria = {
    id: PADARIA,
    nome: 'Padaria Bom Grão',
    documento: null,
    observacoes: null,
    ativo: true,
    versao: '1',
    razaoSocial: null,
    inscricaoEstadual: null,
    email: null,
    telefone: null,
    endereco: null,
    responsavel: { id: RAFAEL, nome: 'Rafael Moura' },
    etiquetas: [],
    contatos: [
        {
            id: 'k1',
            nome: 'Marta Souza',
            email: 'marta@bomgrao.com',
            telefone: null,
            cargo: 'Gerente',
            ativo: true,
            temConta: false,
            acessoPortal: null,
        },
        {
            id: 'k2',
            nome: 'Antigo',
            email: 'antigo@bomgrao.com',
            telefone: null,
            cargo: null,
            ativo: false,
            temConta: false,
            acessoPortal: null,
        },
    ],
}

const contrato = (id: string, tipo: string) => ({
    id,
    clienteId: PADARIA,
    clienteNome: 'Padaria Bom Grão',
    tipo,
    inicio: '2026-01-01',
    fim: null,
    franquiaMinutos: null,
    status: 'ATIVO',
})

const app = (permissoes: string[], rotas: Rotas = {}) =>
    comPerfil(permissoes, {
        'GET /api/v1/solicitacoes': () => pagina([]),
        'GET /api/v1/equipe': () =>
            json([
                { id: perfil.usuarioId, nome: 'Ana Prado' },
                { id: RAFAEL, nome: 'Rafael Moura' },
            ]),
        'GET /api/v1/categorias': () => json([]),
        'GET /api/v1/clientes': (_, url) =>
            pagina(url.searchParams.get('busca') === 'pada' ? [{ ...padaria, contatos: undefined }] : []),
        [`GET /api/v1/clientes/${PADARIA}`]: () => json(padaria),
        'GET /api/v1/contratos': () => pagina([contrato('k9', 'BANCO_HORAS')]),
        ...rotas,
    })

const TODAS = ['solicitacao.ver', 'solicitacao.criar', 'cliente.ver', 'contrato.ver']

describe('nova solicitação', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('abre pela fila, puxa o responsável do cliente e vai para a solicitação criada', async () => {
        const chamadas = app(TODAS, {
            'POST /api/v1/solicitacoes': () => json({ id: 's9', numero: 129 }, 201),
        })
        const roteador = await abrir('/solicitacoes')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('button', { name: 'Nova solicitação' }))
        const janela = await screen.findByRole('dialog', { name: 'Nova solicitação' })
        expect(roteador.state.location.search).toEqual({ nova: true })

        await usuario.type(within(janela).getByRole('combobox', { name: 'Cliente' }), 'pada')
        await usuario.click(await screen.findByRole('option', { name: 'Padaria Bom Grão' }))
        await vi.waitFor(() =>
            expect(within(janela).getByRole('combobox', { name: 'Responsável' })).toHaveValue('Rafael Moura'),
        )
        expect(within(janela).getByText(/contrato banco de horas ativo/)).toBeInTheDocument()

        await usuario.click(within(janela).getByRole('combobox', { name: 'Contato' }))
        expect(screen.queryByRole('option', { name: /Antigo/ })).not.toBeInTheDocument()
        await usuario.click(await screen.findByRole('option', { name: /Marta Souza/ }))
        await usuario.type(within(janela).getByLabelText('Título'), 'Forno não liga')
        await usuario.click(within(janela).getByRole('button', { name: 'Abrir solicitação' }))

        await vi.waitFor(() => expect(roteador.state.location.pathname).toBe('/solicitacoes/s9'))
        expect(await screen.findByText('Solicitação #129 aberta.')).toBeInTheDocument()
        expect(chamadas.find((c) => c.metodo === 'POST' && c.caminho === '/api/v1/solicitacoes')?.corpo).toEqual({
            clienteId: PADARIA,
            contatoId: 'k1',
            contratoId: null,
            titulo: 'Forno não liga',
            descricao: null,
            prioridade: 'MEDIA',
            categoriaId: null,
            responsavelId: RAFAEL,
        })
    })

    it('com mais de um contrato ativo, deixa escolher', async () => {
        app(TODAS, {
            'GET /api/v1/contratos': () => pagina([contrato('k9', 'BANCO_HORAS'), contrato('k8', 'MENSAL_FIXO')]),
        })
        await abrir(`/solicitacoes?nova=1&cliente=${PADARIA}`)
        const janela = await screen.findByRole('dialog', { name: 'Nova solicitação' })
        expect(await within(janela).findByRole('combobox', { name: 'Contrato' })).toBeInTheDocument()
    })

    it('o Ctrl+K oferece criar e abre a janela', async () => {
        app(TODAS)
        const roteador = await abrir('/')
        await screen.findByRole('heading', { level: 1 })
        const usuario = userEvent.setup()
        await usuario.keyboard('{Control>}k{/Control}')
        await usuario.type(await screen.findByRole('searchbox', { name: 'Buscar' }), 'nova')
        await usuario.click(await screen.findByRole('menuitem', { name: 'Nova solicitação' }))
        expect(await screen.findByRole('dialog', { name: 'Nova solicitação' })).toBeInTheDocument()
        expect(roteador.state.location.pathname).toBe('/solicitacoes')
    })

    it('sem permissão de criar, nem botão nem janela', async () => {
        app(['solicitacao.ver'])
        await abrir('/solicitacoes?nova=1')
        await screen.findByText('Nada em aberto')
        expect(screen.queryByRole('button', { name: 'Nova solicitação' })).not.toBeInTheDocument()
        expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    })
})
