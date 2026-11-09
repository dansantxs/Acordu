import { fireEvent, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, json, perfil, type Rotas } from '@/testes/app'

const ID = '0199f0a0-0000-7000-8000-0000000000b1'
const RAFAEL = '0199f0a0-0000-7000-8000-000000000002'

const tarefa = (mudancas: Record<string, unknown> = {}) => ({
    id: ID,
    versao: '3',
    titulo: 'Trocar resistência',
    descricao: 'Forno 2.',
    status: 'A_FAZER',
    clienteId: 'c1',
    solicitacaoId: 's1',
    projetoId: null,
    recorrenciaId: null,
    responsavel: null,
    prazo: '2026-10-25',
    estimativaMinutos: 60,
    visivelCliente: false,
    planejadaPara: null,
    apontadoMinutos: 30,
    checklist: [{ id: 'i1', ordem: 1, texto: 'Comprar peça', feito: false }],
    concluidaEm: null,
    clienteNome: 'Padaria Bom Grão',
    solicitacaoNumero: 1042,
    projetoNome: null,
    ...mudancas,
})

const resumo = {
    id: ID,
    titulo: 'Trocar resistência',
    status: 'A_FAZER',
    clienteId: 'c1',
    solicitacaoId: 's1',
    projetoId: null,
    responsavel: null,
    prazo: null,
    estimativaMinutos: null,
    visivelCliente: false,
    planejadaPara: null,
    clienteNome: 'Padaria Bom Grão',
    solicitacaoNumero: 1042,
    projetoNome: null,
    concluidaEm: null,
}

const app = (permissoes: string[], rotas: Rotas = {}) =>
    comPerfil(permissoes, {
        'GET /api/v1/tarefas': () => json({ itens: [resumo], pagina: 1, tamanhoPagina: 50, total: 1 }),
        [`GET /api/v1/tarefas/${ID}`]: () => json(tarefa()),
        [`GET /api/v1/tarefas/${ID}/comentarios`]: () => json([]),
        [`GET /api/v1/tarefas/${ID}/seguidores`]: () => json([]),
        'GET /api/v1/equipe': () =>
            json([
                { id: perfil.usuarioId, nome: 'Ana Prado' },
                { id: RAFAEL, nome: 'Rafael Moura' },
            ]),
        ...rotas,
    })

const EDITAR = ['tarefa.ver', 'tarefa.editar', 'solicitacao.ver', 'cliente.ver']
const painel = () => screen.findByRole('complementary', { name: 'Trocar resistência' })

describe('detalhe da tarefa', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('abre da lista em painel, com a origem, e fecha com Esc mantendo os filtros', async () => {
        app(['tarefa.ver', 'solicitacao.ver'])
        const roteador = await abrir('/tarefas?responsavel=eu')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('link', { name: 'Trocar resistência' }))

        const aside = await painel()
        expect(roteador.state.location.pathname).toBe(`/tarefas/${ID}`)
        expect(within(aside).getByRole('link', { name: 'solicitação #1042' })).toBeInTheDocument()
        expect(within(aside).getByText('Padaria Bom Grão')).toBeInTheDocument()
        expect(within(aside).getByText('30min')).toBeInTheDocument()
        expect(within(aside).getByText('Comprar peça')).toBeInTheDocument()
        // Só vê: sem ações nem campos.
        expect(within(aside).queryByRole('button', { name: 'Iniciar' })).not.toBeInTheDocument()
        expect(within(aside).queryByRole('combobox')).not.toBeInTheDocument()

        await usuario.keyboard('{Escape}')
        await vi.waitFor(() => expect(roteador.state.location.pathname).toBe('/tarefas'))
        expect(roteador.state.location.search).toEqual({ responsavel: 'eu' })
    })

    it('inicia, mostra concluir e voltar, e cancela com confirmação', async () => {
        let atual = tarefa()
        const chamadas = app(EDITAR, {
            [`GET /api/v1/tarefas/${ID}`]: () => json(atual),
            [`POST /api/v1/tarefas/${ID}/inicio`]: () => {
                atual = tarefa({ status: 'FAZENDO', versao: '4' })
                return json(atual)
            },
            [`POST /api/v1/tarefas/${ID}/cancelamento`]: () => {
                atual = tarefa({ status: 'CANCELADA', versao: '5' })
                return json(atual)
            },
        })
        await abrir(`/tarefas/${ID}`)
        const aside = await painel()
        const usuario = userEvent.setup()

        await usuario.click(within(aside).getByRole('button', { name: 'Iniciar' }))
        expect(await screen.findByText('Tarefa fazendo.')).toBeInTheDocument()
        expect(chamadas.find((c) => c.caminho.endsWith('/inicio'))?.ifMatch).toBe('"3"')
        expect(within(aside).getByRole('button', { name: 'Concluir' })).toBeInTheDocument()
        expect(within(aside).getByRole('button', { name: 'Voltar para a fazer' })).toBeInTheDocument()

        await usuario.click(within(aside).getByRole('button', { name: 'Cancelar tarefa' }))
        const janela = await screen.findByRole('dialog', { name: 'Cancelar esta tarefa?' })
        await usuario.click(within(janela).getByRole('button', { name: 'Cancelar tarefa' }))
        await vi.waitFor(() =>
            expect(within(aside).queryByRole('button', { name: 'Concluir' })).not.toBeInTheDocument(),
        )
        expect(chamadas.find((c) => c.caminho.endsWith('/cancelamento'))?.ifMatch).toBe('"4"')
    })

    it('troca o responsável com desfazer e salva prazo e estimativa pelo formulário inteiro', async () => {
        let atual = tarefa()
        const chamadas = app(EDITAR, {
            [`GET /api/v1/tarefas/${ID}`]: () => json(atual),
            [`PATCH /api/v1/tarefas/${ID}`]: (corpo) => {
                const c = corpo as Record<string, unknown>
                atual = tarefa({
                    ...c,
                    responsavel: c.responsavelId ? { id: RAFAEL, nome: 'Rafael Moura' } : null,
                    versao: String(Number(atual.versao) + 1),
                })
                return json(atual)
            },
        })
        await abrir(`/tarefas/${ID}`)
        const aside = await painel()
        const usuario = userEvent.setup()
        const patches = () => chamadas.filter((c) => c.metodo === 'PATCH')

        await usuario.click(within(aside).getByRole('combobox', { name: 'Responsável' }))
        await usuario.click(await screen.findByRole('option', { name: 'Rafael Moura' }))
        expect(await screen.findByText('Responsável: Rafael Moura.')).toBeInTheDocument()
        expect(patches()[0]).toMatchObject({
            ifMatch: '"3"',
            corpo: {
                titulo: 'Trocar resistência',
                descricao: 'Forno 2.',
                responsavelId: RAFAEL,
                prazo: '2026-10-25',
                estimativaMinutos: 60,
                visivelCliente: false,
                planejadaPara: null,
            },
        })
        await usuario.click(screen.getByRole('button', { name: 'Desfazer' }))
        await vi.waitFor(() => expect(patches()).toHaveLength(2))
        expect(patches()[1]).toMatchObject({ ifMatch: '"4"', corpo: { responsavelId: null } })

        fireEvent.change(within(aside).getByLabelText('Prazo'), { target: { value: '2026-10-30' } })
        await vi.waitFor(() => expect(patches()).toHaveLength(3))
        expect(patches()[2]?.corpo).toMatchObject({ prazo: '2026-10-30' })

        const estimativa = within(aside).getByLabelText('Estimativa')
        await usuario.clear(estimativa)
        await usuario.type(estimativa, 'xyz{Enter}')
        expect(within(aside).getByText(/Não entendi/)).toBeInTheDocument()
        await usuario.clear(estimativa)
        await usuario.type(estimativa, '1:30{Enter}')
        await vi.waitFor(() => expect(patches()).toHaveLength(4))
        expect(patches()[3]?.corpo).toMatchObject({ estimativaMinutos: 90 })
    })

    it('checklist: adiciona, marca e tira itens', async () => {
        const chamadas = app(EDITAR, {
            [`POST /api/v1/tarefas/${ID}/itens`]: () =>
                json({ id: 'i2', ordem: 2, texto: 'Instalar', feito: false }, 201),
            [`PATCH /api/v1/tarefas/${ID}/itens/i1`]: () =>
                json({ id: 'i1', ordem: 1, texto: 'Comprar peça', feito: true }),
            [`DELETE /api/v1/tarefas/${ID}/itens/i1`]: () => new Response(null, { status: 204 }),
        })
        await abrir(`/tarefas/${ID}`)
        const aside = await painel()
        const usuario = userEvent.setup()

        await usuario.type(within(aside).getByLabelText('Novo item da checklist'), 'Instalar{Enter}')
        await vi.waitFor(() =>
            expect(chamadas.find((c) => c.metodo === 'POST' && c.caminho.endsWith('/itens'))?.corpo).toEqual({
                texto: 'Instalar',
                feito: null,
            }),
        )
        await usuario.click(within(aside).getByRole('checkbox', { name: 'Comprar peça' }))
        await vi.waitFor(() =>
            expect(chamadas.find((c) => c.metodo === 'PATCH')?.corpo).toEqual({ texto: 'Comprar peça', feito: true }),
        )
        await usuario.click(within(aside).getByRole('button', { name: 'Tirar "Comprar peça"' }))
        await vi.waitFor(() => expect(chamadas.some((c) => c.metodo === 'DELETE')).toBe(true))
    })

    it('comenta com menção, exclui o próprio comentário e segue a tarefa', async () => {
        let comentarios = [
            {
                id: 'c1',
                autor: { id: RAFAEL, nome: 'Rafael Moura' },
                texto: 'Peça chega amanhã.',
                registradoEm: new Date().toISOString(),
                anexos: [],
            },
        ]
        let seguindo = false
        const chamadas = app(EDITAR, {
            [`GET /api/v1/tarefas/${ID}/comentarios`]: () => json(comentarios),
            [`POST /api/v1/tarefas/${ID}/comentarios`]: (corpo) => {
                const novo = {
                    id: 'c2',
                    autor: { id: perfil.usuarioId, nome: 'Ana Prado' },
                    texto: (corpo as { texto: string }).texto,
                    registradoEm: new Date().toISOString(),
                    anexos: [],
                }
                comentarios = [...comentarios, novo]
                return json(novo, 201)
            },
            [`DELETE /api/v1/tarefas/${ID}/comentarios/c2`]: () => {
                comentarios = comentarios.filter((c) => c.id !== 'c2')
                return new Response(null, { status: 204 })
            },
            [`GET /api/v1/tarefas/${ID}/seguidores`]: () =>
                json(seguindo ? [{ id: perfil.usuarioId, nome: 'Ana Prado' }] : []),
            [`PUT /api/v1/tarefas/${ID}/seguidores/eu`]: () => {
                seguindo = true
                return new Response(null, { status: 204 })
            },
        })
        await abrir(`/tarefas/${ID}`)
        const aside = await painel()
        const secao = within(await within(aside).findByRole('region', { name: 'Comentários' }))
        const usuario = userEvent.setup()

        expect(await secao.findByText('Peça chega amanhã.')).toBeInTheDocument()
        // Só o autor exclui.
        expect(secao.queryByRole('button', { name: 'Excluir' })).not.toBeInTheDocument()

        const caixa = secao.getByRole('combobox', { name: 'Comentário' })
        await usuario.type(caixa, 'Valeu @raf')
        await usuario.click(await screen.findByRole('option', { name: 'Rafael Moura' }))
        await usuario.click(secao.getByRole('button', { name: 'Comentar' }))
        await vi.waitFor(() =>
            expect(chamadas.find((c) => c.metodo === 'POST' && c.caminho.endsWith('/comentarios'))?.corpo).toEqual({
                texto: `Valeu @{${RAFAEL}}`,
            }),
        )
        expect(await secao.findByText('@Rafael Moura')).toBeInTheDocument()

        await usuario.click(secao.getByRole('button', { name: 'Excluir' }))
        const janela = await screen.findByRole('dialog', { name: 'Excluir o comentário?' })
        await usuario.click(within(janela).getByRole('button', { name: 'Excluir comentário' }))
        expect(await screen.findByText('Comentário excluído.')).toBeInTheDocument()
        await vi.waitFor(() => expect(secao.queryByText('@Rafael Moura')).not.toBeInTheDocument())

        await usuario.click(within(aside).getByRole('button', { name: 'Seguir' }))
        expect(await within(aside).findByText('você')).toBeInTheDocument()
    })

    describe('última tarefa da solicitação (F11-15 a F11-17)', () => {
        const OUTRA_ABERTA = { id: 't0', titulo: 'Diagnóstico', status: 'A_FAZER' }
        const solicitacaoComTarefas = (status: string, outra = { ...OUTRA_ABERTA, status: 'CONCLUIDA' }) => ({
            id: 's1',
            numero: 1042,
            status,
            tarefas: [{ id: ID, titulo: 'Trocar resistência', status: 'CONCLUIDA' }, outra],
        })
        const concluir = (permissoes: string[], statusSolicitacao = 'EM_ANDAMENTO', outra?: typeof OUTRA_ABERTA) => {
            let atual = tarefa({ status: 'FAZENDO', versao: '4' })
            return app(permissoes, {
                [`GET /api/v1/tarefas/${ID}`]: () => json(atual),
                [`POST /api/v1/tarefas/${ID}/conclusao`]: () => {
                    atual = tarefa({ status: 'CONCLUIDA', versao: '5' })
                    return json(atual)
                },
                'GET /api/v1/solicitacoes/s1': () => json(solicitacaoComTarefas(statusSolicitacao, outra)),
            })
        }

        it('sugere resolver, e o botão leva à janela de resolução', async () => {
            const chamadas = concluir([...EDITAR, 'solicitacao.editar'])
            const roteador = await abrir(`/tarefas/${ID}`)
            const usuario = userEvent.setup()
            await usuario.click(within(await painel()).getByRole('button', { name: 'Concluir' }))

            expect(
                await screen.findByText('Era a última tarefa da solicitação #1042. Resolver a solicitação agora?'),
            ).toBeInTheDocument()
            // F11-16: o aviso sozinho não resolve nada.
            expect(chamadas.some((c) => c.metodo === 'POST' && c.caminho.endsWith('/resolucao'))).toBe(false)
            await usuario.click(screen.getByRole('button', { name: 'Resolver' }))
            await vi.waitFor(() => expect(roteador.state.location.pathname).toBe('/solicitacoes/s1'))
            expect(roteador.state.location.search).toMatchObject({ resolver: true })
        })

        it.each([
            ['sem editar a solicitação (F11-17)', EDITAR, 'EM_ANDAMENTO', undefined],
            ['com ela já resolvida', [...EDITAR, 'solicitacao.editar'], 'RESOLVIDA', undefined],
            ['com outra tarefa ainda aberta (F11-15)', [...EDITAR, 'solicitacao.editar'], 'EM_ANDAMENTO', OUTRA_ABERTA],
        ])('%s, não sugere', async (_, permissoes, status, outra) => {
            const chamadas = concluir(permissoes, status, outra)
            await abrir(`/tarefas/${ID}`)
            const usuario = userEvent.setup()
            await usuario.click(within(await painel()).getByRole('button', { name: 'Concluir' }))
            expect(await screen.findByText('Tarefa concluída.')).toBeInTheDocument()
            if (permissoes.includes('solicitacao.editar'))
                await vi.waitFor(() => expect(chamadas.some((c) => c.caminho === '/api/v1/solicitacoes/s1')).toBe(true))
            // Depois da consulta, o aviso teria vindo em seguida; espera um pouco e confere que não veio.
            await expect(screen.findByText(/Era a última tarefa/, {}, { timeout: 300 })).rejects.toThrow()
        })
    })
})
