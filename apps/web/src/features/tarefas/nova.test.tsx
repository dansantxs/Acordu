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
    describe('iniciar o cronômetro junto (F12-09 a F12-12)', () => {
        const comCronometro = (
            permissoes: string[],
            rotas: Record<string, (corpo: unknown, url: URL) => Response | Promise<Response>> = {},
        ) =>
            comPerfil(['tarefa.ver', 'tarefa.criar', ...permissoes], {
                'GET /api/v1/tarefas': () => pagina([]),
                'GET /api/v1/equipe': () => json([]),
                'GET /api/v1/cronometro': () => new Response(null, { status: 204 }),
                'POST /api/v1/tarefas': (corpo) => criada(corpo),
                '/api/v1/tarefas/t9': () => json({ id: 't9', titulo: 'Revisar cardápio' }),
                'POST /api/v1/cronometro/inicio': () =>
                    json(
                        {
                            id: 'c1',
                            tarefa: { id: 't9', nome: 'Revisar cardápio' },
                            iniciadoEm: daquiA(0),
                            decorridoMinutos: 0,
                        },
                        201,
                    ),
                ...rotas,
            })
        const criar = async (marcar: boolean) => {
            const roteador = await abrir('/tarefas')
            const usuario = userEvent.setup()
            await usuario.click(await screen.findByRole('button', { name: 'Nova tarefa' }))
            const janela = await screen.findByRole('dialog', { name: 'Nova tarefa' })
            await usuario.type(within(janela).getByLabelText('Título'), 'Revisar cardápio')
            const marcador = within(janela).getByRole('checkbox', { name: 'Iniciar o cronômetro' })
            expect(marcador).not.toBeChecked()
            if (marcar) await usuario.click(marcador)
            await usuario.click(within(janela).getByRole('button', { name: 'Criar tarefa' }))
            return roteador
        }

        it('marcado, cria a tarefa e inicia nela', async () => {
            let ativo = false
            const cronometro = {
                id: 'c1',
                tarefa: { id: 't9', nome: 'Revisar cardápio' },
                iniciadoEm: daquiA(0),
                decorridoMinutos: 0,
            }
            const chamadas = comCronometro(['apontamento.criar'], {
                'GET /api/v1/cronometro': () => (ativo ? json(cronometro) : new Response(null, { status: 204 })),
                'POST /api/v1/cronometro/inicio': () => {
                    ativo = true
                    return json(cronometro, 201)
                },
            })
            await criar(true)
            expect(
                await screen.findByText('Tarefa "Revisar cardápio" criada, com o cronômetro correndo.'),
            ).toBeInTheDocument()
            const posts = chamadas
                .filter((c) => c.metodo === 'POST' && !c.caminho.startsWith('/api/v1/auth'))
                .map((c) => c.caminho)
            expect(posts).toEqual(['/api/v1/tarefas', '/api/v1/cronometro/inicio'])
            expect(chamadas.find((c) => c.caminho === '/api/v1/cronometro/inicio')?.corpo).toMatchObject({
                tarefaId: 't9',
            })
            // O cronômetro é consultado de novo depois do início (a capa lateral passa a mostrá-lo).
            const inicio = chamadas.findIndex((c) => c.caminho === '/api/v1/cronometro/inicio')
            await vi.waitFor(() =>
                expect(chamadas.slice(inicio + 1).some((c) => c.caminho === '/api/v1/cronometro')).toBe(true),
            )
        })

        it('desmarcado (padrão), só cria', async () => {
            const chamadas = comCronometro(['apontamento.criar'])
            await criar(false)
            expect(await screen.findByText('Tarefa "Revisar cardápio" criada.')).toBeInTheDocument()
            expect(chamadas.some((c) => c.caminho === '/api/v1/cronometro/inicio')).toBe(false)
        })

        it('sem apontamento.criar, a opção não aparece e o cronômetro nem é consultado', async () => {
            const chamadas = comCronometro([])
            await abrir('/tarefas')
            const usuario = userEvent.setup()
            await usuario.click(await screen.findByRole('button', { name: 'Nova tarefa' }))
            const janela = await screen.findByRole('dialog', { name: 'Nova tarefa' })
            expect(within(janela).queryByRole('checkbox', { name: 'Iniciar o cronômetro' })).not.toBeInTheDocument()
            expect(chamadas.some((c) => c.caminho === '/api/v1/cronometro')).toBe(false)
        })

        it('com outro cronômetro correndo, vem desabilitada e diz onde', async () => {
            comCronometro(['apontamento.criar'], {
                'GET /api/v1/cronometro': () =>
                    json({
                        id: 'c0',
                        tarefa: { id: 't0', nome: 'Manutenção' },
                        iniciadoEm: daquiA(-60_000),
                        decorridoMinutos: 1,
                    }),
            })
            await abrir('/tarefas')
            const usuario = userEvent.setup()
            await usuario.click(await screen.findByRole('button', { name: 'Nova tarefa' }))
            const janela = await screen.findByRole('dialog', { name: 'Nova tarefa' })
            expect(await within(janela).findByText('Já há um cronômetro em "Manutenção".')).toBeInTheDocument()
            const marcador = within(janela).getByRole('checkbox', { name: 'Iniciar o cronômetro' })
            expect(marcador).toBeDisabled()
            expect(marcador).toHaveAccessibleDescription('Já há um cronômetro em "Manutenção".')
            expect(marcador).not.toBeChecked()
        })

        it('se iniciar falhar, a tarefa fica criada e o aviso diz que o cronômetro não começou', async () => {
            const chamadas = comCronometro(['apontamento.criar'], {
                'POST /api/v1/cronometro/inicio': () =>
                    json(
                        {
                            status: 409,
                            type: 'https://acordu.app.br/erros/cronometro-ativo',
                            title: 'Cronômetro ativo',
                            detail: 'Já existe um cronômetro ativo.',
                        },
                        409,
                    ),
            })
            const roteador = await criar(true)
            expect(await screen.findByText(/criada, mas o cronômetro não começou/)).toBeInTheDocument()
            // A tarefa continua criada: a janela fecha e a tarefa abre, sem nada desfeito.
            await vi.waitFor(() => expect(roteador.state.location.pathname).toBe('/tarefas/t9'))
            expect(screen.queryByRole('dialog', { name: 'Nova tarefa' })).not.toBeInTheDocument()
            expect(chamadas.some((c) => c.metodo === 'DELETE')).toBe(false)
        })
    })
})
