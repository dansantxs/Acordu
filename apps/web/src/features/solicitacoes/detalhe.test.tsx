import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, daquiA, json, perfil, type Rotas } from '@/testes/app'

const ID = '0199f0a0-0000-7000-8000-0000000000a1'
const RAFAEL = '0199f0a0-0000-7000-8000-000000000002'
const PADARIA = '0199f0a0-0000-7000-8000-0000000000c1'

const solicitacao = (mudancas: Record<string, unknown> = {}) => ({
    id: ID,
    numero: 1042,
    versao: '7',
    titulo: 'Forno não liga',
    descricao: 'Desde ontem à tarde.',
    cliente: { id: PADARIA, nome: 'Padaria Bom Grão' },
    contato: { id: 'k1', nome: 'Marta Souza' },
    contratoId: null,
    categoriaId: null,
    prioridade: 'MEDIA',
    status: 'ABERTA',
    statusExterno: 'RECEBIDA',
    origem: 'PORTAL',
    responsavel: null,
    abertaEm: daquiA(-3_600_000),
    sla: {
        prazoPrimeiraResposta: daquiA(3_600_000),
        primeiraRespostaEm: null,
        prazoResolucao: daquiA(20 * 3_600_000),
        pausado: false,
        situacao: 'NO_PRAZO',
        percentualConsumido: 10,
    },
    situacaoSla: 'NO_PRAZO',
    tempoApontadoMinutos: 90,
    tarefas: [{ id: 't1', titulo: 'Trocar resistência', status: 'FAZENDO' }],
    solucao: null,
    resolvidaEm: null,
    fechadaEm: null,
    canceladaEm: null,
    motivoCancelamento: null,
    ...mudancas,
})

const resumo = {
    id: ID,
    numero: 1042,
    titulo: 'Forno não liga',
    cliente: { id: PADARIA, nome: 'Padaria Bom Grão' },
    prioridade: 'MEDIA',
    status: 'ABERTA',
    responsavel: null,
    abertaEm: daquiA(-3_600_000),
    prazoResolucao: null,
    situacaoSla: 'SEM_SLA',
}

const app = (permissoes: string[], rotas: Rotas = {}) =>
    comPerfil(permissoes, {
        'GET /api/v1/solicitacoes': () => json({ itens: [resumo], pagina: 1, tamanhoPagina: 50, total: 1 }),
        [`GET /api/v1/solicitacoes/${ID}`]: () => json(solicitacao()),
        'GET /api/v1/equipe': () =>
            json([
                { id: perfil.usuarioId, nome: 'Ana Prado' },
                { id: RAFAEL, nome: 'Rafael Moura' },
            ]),
        'GET /api/v1/categorias': () => json([]),
        [`GET /api/v1/solicitacoes/${ID}/seguidores`]: () => json([]),
        [`GET /api/v1/solicitacoes/${ID}/semelhantes`]: () => json([]),
        'GET /api/v1/atividades': () => json({ itens: [], proximo: null }),
        [`GET /api/v1/solicitacoes/${ID}/interacoes`]: () => json([]),
        ...rotas,
    })

const EDITAR = ['solicitacao.ver', 'solicitacao.editar', 'cliente.ver']
const painel = () => screen.findByRole('complementary', { name: 'Forno não liga' })

describe('detalhe da solicitação', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('abre em painel sobre a fila, mantém os filtros e fecha com Esc', async () => {
        app(['solicitacao.ver'])
        const roteador = await abrir('/solicitacoes?prioridade=MEDIA')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('link', { name: 'Forno não liga' }))

        const aside = await painel()
        expect(roteador.state.location.pathname).toBe(`/solicitacoes/${ID}`)
        expect(roteador.state.location.search).toEqual({ prioridade: 'MEDIA' })
        expect(screen.getByRole('heading', { name: 'Solicitações', level: 1 })).toBeInTheDocument()
        expect(within(aside).getByText('#1042')).toBeInTheDocument()
        expect(within(aside).getByText(/Marta Souza · aberta há 1 h pelo portal/)).toBeInTheDocument()
        expect(within(aside).getByText('1h30')).toBeInTheDocument()
        expect(within(aside).getByText('Trocar resistência')).toBeInTheDocument()
        // Só vê: sem ações de status e os campos como texto.
        expect(within(aside).queryByRole('button', { name: 'Iniciar' })).not.toBeInTheDocument()
        expect(within(aside).queryByRole('combobox')).not.toBeInTheDocument()

        await vi.waitFor(() => expect(within(aside).getByRole('heading', { level: 2 })).toHaveFocus())
        await usuario.keyboard('{Escape}')
        await vi.waitFor(() => expect(roteador.state.location.pathname).toBe('/solicitacoes'))
        expect(roteador.state.location.search).toEqual({ prioridade: 'MEDIA' })
        expect(screen.queryByRole('complementary')).not.toBeInTheDocument()
        await vi.waitFor(() => expect(screen.getByRole('link', { name: 'Forno não liga' })).toHaveFocus())
    })

    it('troca o responsável na hora e desfaz pelo aviso', async () => {
        let atual = solicitacao()
        const chamadas = app(EDITAR, {
            [`GET /api/v1/solicitacoes/${ID}`]: () => json(atual),
            [`PATCH /api/v1/solicitacoes/${ID}`]: (corpo) => {
                const c = corpo as { responsavelId: string | null }
                atual = solicitacao({
                    versao: String(Number(atual.versao) + 1),
                    responsavel: c.responsavelId ? { id: RAFAEL, nome: 'Rafael Moura' } : null,
                })
                return json(atual)
            },
        })
        await abrir(`/solicitacoes/${ID}`)
        const aside = await painel()
        const usuario = userEvent.setup()

        await usuario.click(within(aside).getByRole('combobox', { name: 'Responsável' }))
        await usuario.click(await screen.findByRole('option', { name: 'Rafael Moura' }))
        expect(await screen.findByText('Responsável: Rafael Moura.')).toBeInTheDocument()
        const patches = () => chamadas.filter((c) => c.metodo === 'PATCH')
        expect(patches()[0]).toMatchObject({
            ifMatch: '"7"',
            corpo: {
                titulo: 'Forno não liga',
                descricao: 'Desde ontem à tarde.',
                prioridade: 'MEDIA',
                categoriaId: null,
                responsavelId: RAFAEL,
            },
        })

        await usuario.click(screen.getByRole('button', { name: 'Desfazer' }))
        await vi.waitFor(() => expect(patches()).toHaveLength(2))
        expect(patches()[1]).toMatchObject({ ifMatch: '"8"', corpo: { responsavelId: null } })
        await vi.waitFor(() => expect(within(aside).getByRole('combobox', { name: 'Responsável' })).toHaveValue(''))
    })

    it('muda o status direto e resolve com a solução', async () => {
        let atual = solicitacao()
        const chamadas = app(EDITAR, {
            [`GET /api/v1/solicitacoes/${ID}`]: () => json(atual),
            [`POST /api/v1/solicitacoes/${ID}/inicio`]: () => {
                atual = solicitacao({ versao: '8', status: 'EM_ANDAMENTO' })
                return json(atual)
            },
            [`POST /api/v1/solicitacoes/${ID}/resolucao`]: (corpo) => {
                const { solucao } = corpo as { solucao: unknown }
                atual = solicitacao({ versao: '9', status: 'RESOLVIDA', solucao, resolvidaEm: daquiA(0) })
                return json(atual)
            },
        })
        await abrir(`/solicitacoes/${ID}`)
        const aside = await painel()
        const usuario = userEvent.setup()

        await usuario.click(within(aside).getByRole('button', { name: 'Iniciar' }))
        expect(await screen.findByText('Solicitação #1042: em andamento.')).toBeInTheDocument()
        expect(chamadas.find((c) => c.caminho.endsWith('/inicio'))?.ifMatch).toBe('"7"')

        await usuario.click(within(aside).getByRole('button', { name: 'Resolver' }))
        const janela = await screen.findByRole('dialog', { name: 'Resolver solicitação' })
        await usuario.type(within(janela).getByLabelText('Solução'), 'Resistência trocada.')
        await usuario.type(within(janela).getByLabelText('Causa'), 'Queimou')
        await usuario.click(within(janela).getByRole('button', { name: 'Resolver' }))

        expect(await within(aside).findByText('Resistência trocada.')).toBeInTheDocument()
        expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
        expect(chamadas.find((c) => c.caminho.endsWith('/resolucao'))).toMatchObject({
            ifMatch: '"8"',
            corpo: { solucao: { texto: 'Resistência trocada.', problema: null, causa: 'Queimou' } },
        })
        expect(within(aside).getByRole('button', { name: 'Reabrir' })).toBeInTheDocument()
        expect(within(aside).queryByRole('combobox', { name: 'Prioridade' })).not.toBeInTheDocument()
    })

    it('cancelar pede o motivo e avisa que não tem volta', async () => {
        const chamadas = app(EDITAR, {
            [`POST /api/v1/solicitacoes/${ID}/cancelamento`]: () =>
                json(solicitacao({ versao: '8', status: 'CANCELADA', motivoCancelamento: 'Duplicada' })),
        })
        await abrir(`/solicitacoes/${ID}`)
        const aside = await painel()
        const usuario = userEvent.setup()

        await usuario.click(within(aside).getByRole('button', { name: 'Cancelar solicitação' }))
        const janela = await screen.findByRole('dialog', { name: 'Cancelar a solicitação #1042?' })
        expect(within(janela).getByText(/definitivo/)).toBeInTheDocument()
        // Esc fecha a janela, não o painel.
        await usuario.keyboard('{Escape}')
        await vi.waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
        expect(screen.getByRole('complementary')).toBeInTheDocument()

        await usuario.click(within(aside).getByRole('button', { name: 'Cancelar solicitação' }))
        const denovo = await screen.findByRole('dialog', { name: 'Cancelar a solicitação #1042?' })
        await usuario.type(within(denovo).getByLabelText('Motivo'), 'Duplicada')
        await usuario.click(within(denovo).getByRole('button', { name: 'Cancelar solicitação' }))

        expect(await within(aside).findByText(/Cancelada.*: Duplicada/)).toBeInTheDocument()
        expect(chamadas.find((c) => c.caminho.endsWith('/cancelamento'))?.corpo).toEqual({ motivo: 'Duplicada' })
        expect(within(aside).queryByRole('button', { name: /Cancelar|Resolver/ })).not.toBeInTheDocument()
    })

    it('segue e deixa de seguir', async () => {
        let seguindo = false
        const chamadas = app(['solicitacao.ver'], {
            [`GET /api/v1/solicitacoes/${ID}/seguidores`]: () =>
                json([
                    { id: RAFAEL, nome: 'Rafael Moura' },
                    ...(seguindo ? [{ id: perfil.usuarioId, nome: 'Ana Prado' }] : []),
                ]),
            [`PUT /api/v1/solicitacoes/${ID}/seguidores/eu`]: () => {
                seguindo = true
                return new Response(null, { status: 204 })
            },
            [`DELETE /api/v1/solicitacoes/${ID}/seguidores/eu`]: () => {
                seguindo = false
                return new Response(null, { status: 204 })
            },
        })
        await abrir(`/solicitacoes/${ID}`)
        const aside = await painel()
        const usuario = userEvent.setup()

        expect(await within(aside).findByText('Rafael Moura')).toBeInTheDocument()
        await usuario.click(within(aside).getByRole('button', { name: 'Seguir' }))
        expect(await within(aside).findByText('Rafael Moura, você')).toBeInTheDocument()
        await usuario.click(within(aside).getByRole('button', { name: 'Deixar de seguir' }))
        expect(await within(aside).findByText('Rafael Moura')).toBeInTheDocument()
        expect(chamadas.filter((c) => c.caminho.endsWith('/seguidores/eu')).map((c) => c.metodo)).toEqual([
            'PUT',
            'DELETE',
        ])
    })

    it('mostra as parecidas já resolvidas e abre uma delas no painel', async () => {
        const OUTRA = '0199f0a0-0000-7000-8000-0000000000a2'
        app(['solicitacao.ver'], {
            [`GET /api/v1/solicitacoes/${ID}/semelhantes`]: () =>
                json([
                    {
                        id: OUTRA,
                        numero: 981,
                        titulo: 'Forno sem aquecer',
                        solucaoResumo: 'Resistência queimada, trocada.',
                        resolvidaEm: daquiA(-86_400_000 * 30),
                        relevancia: 0.71,
                    },
                ]),
            [`GET /api/v1/solicitacoes/${OUTRA}`]: () =>
                json(solicitacao({ id: OUTRA, numero: 981, titulo: 'Forno sem aquecer' })),
            [`GET /api/v1/solicitacoes/${OUTRA}/seguidores`]: () => json([]),
            [`GET /api/v1/solicitacoes/${OUTRA}/semelhantes`]: () => json([]),
            [`GET /api/v1/solicitacoes/${OUTRA}/interacoes`]: () => json([]),
        })
        const roteador = await abrir(`/solicitacoes/${ID}?prioridade=MEDIA`)
        const aside = await painel()
        const parecidas = within(await within(aside).findByRole('region', { name: 'Parecidas já resolvidas' }))
        expect(parecidas.getByText('Resistência queimada, trocada.')).toBeInTheDocument()

        await userEvent.setup().click(parecidas.getByRole('link', { name: /Forno sem aquecer/ }))
        expect(await screen.findByRole('complementary', { name: 'Forno sem aquecer' })).toBeInTheDocument()
        expect(roteador.state.location.pathname).toBe(`/solicitacoes/${OUTRA}`)
        expect(roteador.state.location.search).toEqual({ prioridade: 'MEDIA' })
    })

    it('gera o link do portal para um contato e revoga o antigo', async () => {
        const chamadas = app(['solicitacao.ver', 'cliente.ver', 'portal.gerenciar_acessos'], {
            [`GET /api/v1/clientes/${PADARIA}`]: () =>
                json({
                    id: PADARIA,
                    nome: 'Padaria Bom Grão',
                    contatos: [
                        { id: 'k1', nome: 'Marta Souza', ativo: true },
                        { id: 'k3', nome: 'João Lima', ativo: true },
                    ],
                }),
            [`GET /api/v1/solicitacoes/${ID}/acessos-portal`]: () =>
                json([
                    {
                        acessoId: 'l1',
                        contatoId: 'k3',
                        contatoNome: 'João Lima',
                        emitidoEm: daquiA(-86_400_000),
                        expiraEm: daquiA(86_400_000 * 29),
                    },
                ]),
            [`DELETE /api/v1/solicitacoes/${ID}/acessos-portal/l1`]: () => new Response(null, { status: 204 }),
            [`POST /api/v1/solicitacoes/${ID}/acessos-portal`]: () =>
                json(
                    {
                        acessoId: 'l2',
                        expiraEm: daquiA(86_400_000 * 30),
                        link: 'http://localhost:5173/portal/l/abc123',
                        emailEnviado: true,
                    },
                    201,
                ),
        })
        await abrir(`/solicitacoes/${ID}`)
        const aside = await painel()
        const usuario = userEvent.setup()

        await usuario.click(within(aside).getByRole('button', { name: 'Link do portal' }))
        const janela = await screen.findByRole('dialog', { name: 'Link do portal' })
        expect(await within(janela).findByText('João Lima')).toBeInTheDocument()
        await usuario.click(within(janela).getByRole('button', { name: 'Revogar' }))
        expect(await screen.findByText('Link de João Lima revogado.')).toBeInTheDocument()

        await vi.waitFor(() =>
            expect(within(janela).getByRole('combobox', { name: 'Para quem' })).toHaveValue('Marta Souza'),
        )
        await usuario.click(within(janela).getByRole('button', { name: 'Gerar link' }))
        expect(await within(janela).findByDisplayValue('http://localhost:5173/portal/l/abc123')).toBeInTheDocument()
        expect(within(janela).getByText(/enviado por e-mail/)).toBeInTheDocument()
        expect(chamadas.find((c) => c.metodo === 'POST' && c.caminho.endsWith('/acessos-portal'))?.corpo).toEqual({
            contatoId: 'k1',
            validadeDias: 30,
            enviarEmail: true,
        })
        expect(chamadas.some((c) => c.metodo === 'DELETE')).toBe(true)
    })

    it('troca o contrato e recalcula o prazo', async () => {
        let atual = solicitacao({ contratoId: 'k9' })
        const chamadas = app(['solicitacao.ver', 'solicitacao.editar', 'contrato.ver'], {
            [`GET /api/v1/solicitacoes/${ID}`]: () => json(atual),
            '/api/v1/contratos/k9': () => json({ id: 'k9', tipo: 'BANCO_HORAS', inicio: '2026-01-01' }),
            '/api/v1/contratos/k10': () => json({ id: 'k10', tipo: 'MENSAL_FIXO', inicio: '2026-06-01' }),
            'GET /api/v1/contratos': () =>
                json({
                    itens: [
                        { id: 'k9', tipo: 'BANCO_HORAS', inicio: '2026-01-01' },
                        { id: 'k10', tipo: 'MENSAL_FIXO', inicio: '2026-06-01' },
                    ],
                    pagina: 1,
                    tamanhoPagina: 50,
                    total: 2,
                }),
            [`PUT /api/v1/solicitacoes/${ID}/contrato`]: () => {
                atual = solicitacao({ contratoId: 'k10', versao: '8' })
                return json(atual)
            },
        })
        await abrir(`/solicitacoes/${ID}`)
        const aside = await painel()
        const usuario = userEvent.setup()

        expect(await within(aside).findByText('Banco de horas')).toBeInTheDocument()
        await usuario.click(within(aside).getByRole('button', { name: 'Trocar' }))
        const janela = await screen.findByRole('dialog', { name: 'Trocar contrato' })
        await usuario.click(within(janela).getByRole('combobox', { name: 'Contrato' }))
        await usuario.click(await screen.findByRole('option', { name: /Mensal fixo/ }))
        await usuario.click(within(janela).getByRole('button', { name: 'Trocar contrato' }))

        expect(await screen.findByText('Contrato trocado; o prazo foi recalculado.')).toBeInTheDocument()
        expect(await within(aside).findByText('Mensal fixo')).toBeInTheDocument()
        expect(chamadas.find((c) => c.metodo === 'PUT')).toMatchObject({ ifMatch: '"7"', corpo: { contratoId: 'k10' } })
    })

    it('solicitação inexistente avisa no painel e a fila continua', async () => {
        app(['solicitacao.ver'], {
            [`GET /api/v1/solicitacoes/${ID}`]: () => json({ status: 404, detail: 'Não encontrado.' }, 404),
        })
        await abrir(`/solicitacoes/${ID}`)
        expect(await screen.findByText(/Não encontramos esta solicitação/)).toBeInTheDocument()
        expect(screen.getByRole('link', { name: 'Forno não liga' })).toBeInTheDocument()
    })
})
