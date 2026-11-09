import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { hojeNoFuso, somarDias } from '@/shared/format/datas'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, json, type Rotas } from '@/testes/app'

const PADARIA = '0199f0a0-0000-7000-8000-0000000000c1'
const ID = '0199f0a0-0000-7000-8000-0000000000d1'
const hoje = hojeNoFuso('America/Sao_Paulo')
const pagina = (itens: unknown[]) => json({ itens, pagina: 1, tamanhoPagina: 50, total: itens.length })

const resumo = {
    id: ID,
    nome: 'Campanha de Natal',
    cliente: { id: PADARIA, nome: 'Padaria Bom Grão' },
    contratoId: null,
    inicio: null,
    prazo: somarDias(hoje, -2),
    estimativaMinutos: 1200,
    status: 'EM_ANDAMENTO',
}
const projeto = (mudancas: Record<string, unknown> = {}) => ({
    ...resumo,
    versao: '2',
    progresso: { tarefasConcluidas: 3, tarefasTotal: 5, apontadoMinutos: 600, estimativaMinutos: 1200 },
    ...mudancas,
})

const app = (permissoes: string[], rotas: Rotas = {}) =>
    comPerfil(permissoes, {
        'GET /api/v1/projetos': () => pagina([resumo]),
        [`GET /api/v1/projetos/${ID}`]: () => json(projeto()),
        'GET /api/v1/tarefas': () => pagina([]),
        'GET /api/v1/equipe': () => json([]),
        ...rotas,
    })

const GERENCIA = ['projeto.ver', 'projeto.gerenciar', 'cliente.ver', 'contrato.ver', 'tarefa.criar', 'tarefa.ver']

describe('projetos', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('lista os em andamento com o prazo vencido em destaque', async () => {
        const chamadas = app(['projeto.ver'])
        await abrir('/projetos')
        const linha = (await screen.findByRole('link', { name: 'Campanha de Natal' })).closest('tr')!
        expect(within(linha).getByText('Padaria Bom Grão')).toBeInTheDocument()
        expect(within(linha).getByText('(atrasado)')).toHaveClass('sr-only')
        expect(chamadas.find((c) => c.caminho === '/api/v1/projetos')?.consulta.get('status')).toBe('EM_ANDAMENTO')
        expect(screen.queryByRole('button', { name: 'Novo projeto' })).not.toBeInTheDocument()
    })

    it('cria com cliente, contrato, datas e estimativa em horas, e abre o criado', async () => {
        let tentativa = 0
        const chamadas = app(GERENCIA, {
            'GET /api/v1/clientes': (_, url) =>
                pagina(url.searchParams.get('busca') === 'pada' ? [{ id: PADARIA, nome: 'Padaria Bom Grão' }] : []),
            [`GET /api/v1/clientes/${PADARIA}`]: () => json({ id: PADARIA, nome: 'Padaria Bom Grão', contatos: [] }),
            'GET /api/v1/contratos': () =>
                pagina([{ id: 'k9', tipo: 'BANCO_HORAS', inicio: '2026-01-01', status: 'ATIVO' }]),
            'POST /api/v1/projetos': () => {
                tentativa += 1
                return tentativa === 1
                    ? json({ status: 400, errors: { inicio: ['O início não pode ser depois do prazo.'] } }, 400)
                    : json(projeto(), 201)
            },
        })
        const roteador = await abrir('/projetos')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('button', { name: 'Novo projeto' }))
        const janela = await screen.findByRole('dialog', { name: 'Novo projeto' })

        await usuario.type(within(janela).getByRole('combobox', { name: 'Cliente' }), 'pada')
        await usuario.click(await screen.findByRole('option', { name: 'Padaria Bom Grão' }))
        await usuario.type(within(janela).getByLabelText('Nome'), 'Campanha de Natal')
        await usuario.click(within(janela).getByRole('combobox', { name: 'Contrato' }))
        await usuario.click(await screen.findByRole('option', { name: /Banco de horas/ }))
        await usuario.type(within(janela).getByLabelText('Estimativa total'), '120h')
        await usuario.click(within(janela).getByRole('button', { name: 'Criar projeto' }))

        expect(await within(janela).findByText('O início não pode ser depois do prazo.')).toBeInTheDocument()
        await usuario.click(within(janela).getByRole('button', { name: 'Criar projeto' }))
        await vi.waitFor(() => expect(roteador.state.location.pathname).toBe(`/projetos/${ID}`))
        expect(chamadas.filter((c) => c.metodo === 'POST' && c.caminho === '/api/v1/projetos').at(-1)?.corpo).toEqual({
            clienteId: PADARIA,
            nome: 'Campanha de Natal',
            contratoId: 'k9',
            inicio: null,
            prazo: null,
            estimativaMinutos: 7200,
        })
    })

    it('painel mostra o progresso e as tarefas, cria tarefa no projeto e conclui com confirmação', async () => {
        let atual = projeto()
        const chamadas = app(GERENCIA, {
            [`GET /api/v1/projetos/${ID}`]: () => json(atual),
            'GET /api/v1/tarefas': (_, url) =>
                pagina(
                    url.searchParams.get('projetoId') === ID
                        ? [
                              {
                                  id: 't1',
                                  titulo: 'Arte do cartaz',
                                  status: 'FAZENDO',
                                  responsavel: { id: 'u2', nome: 'Rafael Moura' },
                              },
                          ]
                        : [],
                ),
            'POST /api/v1/tarefas': (corpo) =>
                json({ id: 't2', versao: '1', status: 'A_FAZER', checklist: [], ...(corpo as object) }, 201),
            [`POST /api/v1/projetos/${ID}/conclusao`]: () => {
                atual = projeto({ status: 'CONCLUIDO', versao: '3' })
                return json(atual)
            },
        })
        await abrir(`/projetos/${ID}`)
        const aside = await screen.findByRole('complementary', { name: 'Campanha de Natal' })
        const usuario = userEvent.setup()

        expect(within(aside).getByRole('meter', { name: 'Tarefas concluídas' })).toHaveAttribute('aria-valuenow', '60')
        expect(within(aside).getByText('10h de 20h')).toBeInTheDocument()
        expect(await within(aside).findByRole('link', { name: 'Arte do cartaz' })).toBeInTheDocument()

        await usuario.click(within(aside).getByRole('button', { name: 'Nova tarefa' }))
        const janela = await screen.findByRole('dialog', { name: 'Nova tarefa' })
        expect(within(janela).getByText('Do projeto Campanha de Natal · Padaria Bom Grão')).toBeInTheDocument()
        await usuario.type(within(janela).getByLabelText('Título'), 'Revisar textos')
        await usuario.click(within(janela).getByRole('button', { name: 'Criar tarefa' }))
        await vi.waitFor(() =>
            expect(chamadas.find((c) => c.metodo === 'POST' && c.caminho === '/api/v1/tarefas')?.corpo).toMatchObject({
                projetoId: ID,
                solicitacaoId: null,
                clienteId: null,
                visivelCliente: false,
            }),
        )

        await usuario.click(within(aside).getByRole('button', { name: 'Concluir projeto' }))
        const confirmacao = await screen.findByRole('dialog', { name: 'Concluir o projeto?' })
        await usuario.click(within(confirmacao).getByRole('button', { name: 'Concluir projeto' }))
        expect(await screen.findByText('Projeto concluído.')).toBeInTheDocument()
        expect(chamadas.find((c) => c.caminho.endsWith('/conclusao'))?.ifMatch).toBe('"2"')
        await vi.waitFor(() =>
            expect(within(aside).queryByRole('button', { name: 'Concluir projeto' })).not.toBeInTheDocument(),
        )
    })
    describe('com as tarefas abertas (F11-27, F11-28)', () => {
        const abertas = [
            { id: 't1', titulo: 'Arte do cartaz', status: 'FAZENDO', responsavel: null },
            { id: 't2', titulo: 'Textos', status: 'A_FAZER', responsavel: null },
        ]
        const comAbertas = (permissoes: string[], conclusao: Rotas[string]) => {
            let tarefas = abertas
            return {
                mudarTarefas: (novas: typeof abertas) => (tarefas = novas),
                chamadas: app(permissoes, {
                    'GET /api/v1/tarefas': (_, url) => pagina(url.searchParams.get('projetoId') === ID ? tarefas : []),
                    [`POST /api/v1/projetos/${ID}/conclusao`]: conclusao,
                    [`POST /api/v1/projetos/${ID}/cancelamento`]: () =>
                        json(projeto({ status: 'CANCELADO', versao: '3' })),
                }),
            }
        }

        it('concluir oferece concluir as abertas junto, marcado, e manda as que a pessoa viu', async () => {
            const { chamadas } = comAbertas([...GERENCIA, 'tarefa.editar'], () =>
                json(projeto({ status: 'CONCLUIDO', versao: '3' })),
            )
            await abrir(`/projetos/${ID}`)
            const aside = await screen.findByRole('complementary', { name: 'Campanha de Natal' })
            const usuario = userEvent.setup()
            await within(aside).findByRole('link', { name: 'Textos' })
            await usuario.click(within(aside).getByRole('button', { name: 'Concluir projeto' }))
            const janela = await screen.findByRole('dialog', { name: 'Concluir o projeto?' })
            expect(within(janela).getByText('2 tarefas ainda estão abertas:')).toBeInTheDocument()
            expect(within(janela).getByRole('checkbox', { name: 'Concluir as tarefas abertas junto' })).toBeChecked()
            await usuario.click(within(janela).getByRole('button', { name: 'Concluir projeto' }))
            await vi.waitFor(() =>
                expect(chamadas.find((c) => c.caminho.endsWith('/conclusao'))?.corpo).toEqual({
                    tarefas: { acao: 'CONCLUIR', ids: ['t1', 't2'] },
                }),
            )
        })

        it('cancelar oferece cancelar junto; desmarcado, manda MANTER', async () => {
            const { chamadas } = comAbertas([...GERENCIA, 'tarefa.editar'], () => json(projeto()))
            await abrir(`/projetos/${ID}`)
            const aside = await screen.findByRole('complementary', { name: 'Campanha de Natal' })
            const usuario = userEvent.setup()
            await within(aside).findByRole('link', { name: 'Textos' })
            await usuario.click(within(aside).getByRole('button', { name: 'Cancelar projeto' }))
            const janela = await screen.findByRole('dialog', { name: 'Cancelar o projeto?' })
            await usuario.click(within(janela).getByRole('checkbox', { name: 'Cancelar as tarefas abertas junto' }))
            await usuario.click(within(janela).getByRole('button', { name: 'Cancelar projeto' }))
            await vi.waitFor(() =>
                expect(chamadas.find((c) => c.caminho.endsWith('/cancelamento'))?.corpo).toEqual({
                    tarefas: { acao: 'MANTER', ids: ['t1', 't2'] },
                }),
            )
        })

        it('sem tarefa.editar, só avisa e manda sem tarefas', async () => {
            const { chamadas } = comAbertas(GERENCIA, () => json(projeto({ status: 'CONCLUIDO', versao: '3' })))
            await abrir(`/projetos/${ID}`)
            const aside = await screen.findByRole('complementary', { name: 'Campanha de Natal' })
            const usuario = userEvent.setup()
            await within(aside).findByRole('link', { name: 'Textos' })
            await usuario.click(within(aside).getByRole('button', { name: 'Concluir projeto' }))
            const janela = await screen.findByRole('dialog', { name: 'Concluir o projeto?' })
            expect(within(janela).getByText(/mudar tarefas pede outra permissão/)).toBeInTheDocument()
            await usuario.click(within(janela).getByRole('button', { name: 'Concluir projeto' }))
            await vi.waitFor(() =>
                expect(chamadas.find((c) => c.caminho.endsWith('/conclusao'))?.corpo).toEqual({ tarefas: null }),
            )
        })

        it('lista mudou no meio: mostra o 409 na janela e recebe a lista de agora', async () => {
            let tentativas = 0
            const cenario = comAbertas([...GERENCIA, 'tarefa.editar'], () => {
                tentativas++
                if (tentativas > 1) return json(projeto({ status: 'CONCLUIDO', versao: '3' }))
                cenario.mudarTarefas([...abertas, { id: 't3', titulo: 'Fotos', status: 'A_FAZER', responsavel: null }])
                return json(
                    {
                        status: 409,
                        type: 'https://acordu.app.br/erros/conflito-versao',
                        title: 'Versão divergente',
                        detail: 'A lista de tarefas abertas mudou. Confira e confirme de novo.',
                    },
                    409,
                )
            })
            await abrir(`/projetos/${ID}`)
            const aside = await screen.findByRole('complementary', { name: 'Campanha de Natal' })
            const usuario = userEvent.setup()
            await within(aside).findByRole('link', { name: 'Textos' })
            await usuario.click(within(aside).getByRole('button', { name: 'Concluir projeto' }))
            const janela = await screen.findByRole('dialog', { name: 'Concluir o projeto?' })
            await usuario.click(within(janela).getByRole('button', { name: 'Concluir projeto' }))

            expect(
                await within(janela).findByText('A lista de tarefas abertas mudou. Confira e confirme de novo.'),
            ).toBeInTheDocument()
            expect(await within(janela).findByText('Fotos')).toBeInTheDocument()
            await usuario.click(within(janela).getByRole('button', { name: 'Concluir projeto' }))
            await vi.waitFor(() =>
                expect(cenario.chamadas.filter((c) => c.caminho.endsWith('/conclusao')).at(-1)?.corpo).toEqual({
                    tarefas: { acao: 'CONCLUIR', ids: ['t1', 't2', 't3'] },
                }),
            )
        })

        it('com ?concluir=true, abre a confirmação e limpa o endereço (F11-28)', async () => {
            comAbertas(GERENCIA, () => json(projeto()))
            const roteador = await abrir(`/projetos/${ID}?concluir=true`)
            expect(await screen.findByRole('dialog', { name: 'Concluir o projeto?' })).toBeInTheDocument()
            await vi.waitFor(() => expect(roteador.state.location.search).not.toHaveProperty('concluir'))
        })
    })
})
