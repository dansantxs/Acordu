import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, daquiA, json, perfil } from '@/testes/app'

const PADARIA = '0199f0a0-0000-7000-8000-0000000000c1'
const SOL = '0199f0a0-0000-7000-8000-0000000000a1'
const pagina = (itens: unknown[]) => json({ itens, pagina: 1, tamanhoPagina: 50, total: itens.length })
const criada = (corpo: unknown) =>
    json({ id: 't9', versao: '1', status: 'A_FAZER', checklist: [], apontadoMinutos: 0, ...(corpo as object) }, 201)

describe('nova tarefa', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('pela tela de tarefas, avulsa de um cliente, e abre a criada', async () => {
        const chamadas = comPerfil(['tarefa.ver', 'tarefa.criar', 'cliente.ver'], {
            'GET /api/v1/tarefas': () => pagina([]),
            'GET /api/v1/equipe': () => json([{ id: perfil.usuarioId, nome: 'Ana Prado' }]),
            'GET /api/v1/clientes': (_, url) =>
                pagina(url.searchParams.get('busca') === 'pada' ? [{ id: PADARIA, nome: 'Padaria Bom Grão' }] : []),
            [`GET /api/v1/clientes/${PADARIA}`]: () => json({ id: PADARIA, nome: 'Padaria Bom Grão', contatos: [] }),
            'POST /api/v1/tarefas': (corpo) => criada(corpo),
            '/api/v1/tarefas/t9': () => json({ id: 't9', titulo: 'Revisar cardápio' }),
        })
        const roteador = await abrir('/tarefas')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('button', { name: 'Nova tarefa' }))
        const janela = await screen.findByRole('dialog', { name: 'Nova tarefa' })
        expect(roteador.state.location.search).toEqual({ nova: true })

        await usuario.type(within(janela).getByLabelText('Título'), 'Revisar cardápio')
        // Sem cliente, a opção do portal não aparece.
        expect(within(janela).queryByRole('checkbox')).not.toBeInTheDocument()
        await usuario.type(within(janela).getByRole('combobox', { name: 'Cliente' }), 'pada')
        await usuario.click(await screen.findByRole('option', { name: 'Padaria Bom Grão' }))
        await usuario.type(within(janela).getByLabelText('Estimativa'), '2h')
        await usuario.click(within(janela).getByRole('checkbox', { name: /cliente vê/ }))
        await usuario.click(within(janela).getByRole('button', { name: 'Criar tarefa' }))

        await vi.waitFor(() => expect(roteador.state.location.pathname).toBe('/tarefas/t9'))
        expect(await screen.findByText('Tarefa "Revisar cardápio" criada.')).toBeInTheDocument()
        expect(chamadas.find((c) => c.metodo === 'POST' && c.caminho === '/api/v1/tarefas')?.corpo).toEqual({
            titulo: 'Revisar cardápio',
            descricao: null,
            solicitacaoId: null,
            projetoId: null,
            clienteId: PADARIA,
            responsavelId: null,
            prazo: null,
            planejadaPara: null,
            estimativaMinutos: 120,
            visivelCliente: true,
        })
    })

    it('pelo painel da solicitação, herdando o cliente e visível no portal por padrão', async () => {
        const chamadas = comPerfil(['solicitacao.ver', 'tarefa.criar', 'tarefa.ver'], {
            'GET /api/v1/solicitacoes': () => pagina([]),
            [`GET /api/v1/solicitacoes/${SOL}`]: () =>
                json({
                    id: SOL,
                    numero: 1042,
                    versao: '1',
                    titulo: 'Forno não liga',
                    descricao: null,
                    cliente: { id: PADARIA, nome: 'Padaria Bom Grão' },
                    contato: null,
                    contratoId: null,
                    categoriaId: null,
                    prioridade: 'MEDIA',
                    status: 'EM_ANDAMENTO',
                    statusExterno: 'EM_ANDAMENTO',
                    origem: 'INTERNA',
                    responsavel: null,
                    abertaEm: daquiA(-3_600_000),
                    sla: null,
                    situacaoSla: 'SEM_SLA',
                    tempoApontadoMinutos: 0,
                    tarefas: [],
                    solucao: null,
                    resolvidaEm: null,
                    fechadaEm: null,
                    canceladaEm: null,
                    motivoCancelamento: null,
                }),
            'GET /api/v1/equipe': () => json([]),
            'GET /api/v1/categorias': () => json([]),
            [`GET /api/v1/solicitacoes/${SOL}/seguidores`]: () => json([]),
            [`GET /api/v1/solicitacoes/${SOL}/semelhantes`]: () => json([]),
            [`GET /api/v1/solicitacoes/${SOL}/interacoes`]: () => json([]),
            'GET /api/v1/atividades': () => json({ itens: [], proximo: null }),
            'POST /api/v1/tarefas': (corpo) => criada(corpo),
        })
        const roteador = await abrir(`/solicitacoes/${SOL}`)
        const aside = await screen.findByRole('complementary', { name: 'Forno não liga' })
        const usuario = userEvent.setup()
        expect(within(aside).getByText('Nenhuma tarefa ainda.')).toBeInTheDocument()

        await usuario.click(within(aside).getByRole('button', { name: 'Nova tarefa' }))
        const janela = await screen.findByRole('dialog', { name: 'Nova tarefa' })
        expect(within(janela).getByText('Da solicitação #1042 · Padaria Bom Grão')).toBeInTheDocument()
        expect(within(janela).getByRole('checkbox', { name: /cliente vê/ })).toBeChecked()
        await usuario.type(within(janela).getByLabelText('Título'), 'Trocar resistência')
        await usuario.click(within(janela).getByRole('button', { name: 'Criar tarefa' }))

        expect(await screen.findByText('Tarefa "Trocar resistência" criada.')).toBeInTheDocument()
        expect(roteador.state.location.pathname).toBe(`/solicitacoes/${SOL}`)
        expect(chamadas.find((c) => c.metodo === 'POST' && c.caminho === '/api/v1/tarefas')?.corpo).toMatchObject({
            solicitacaoId: SOL,
            clienteId: null,
            visivelCliente: true,
        })
    })
})
