import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { hojeNoFuso } from '@/shared/format/datas'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, json, perfil, type Chamada, type Rotas } from '@/testes/app'

const RAFAEL = '0199f0a0-0000-7000-8000-000000000002'
const pagina = (itens: unknown[]) => json({ itens, pagina: 1, tamanhoPagina: 50, total: itens.length })
const ontem = hojeNoFuso('America/Sao_Paulo', new Date(Date.now() - 86_400_000))

const tarefa = (mudancas: Record<string, unknown>) => ({
    id: crypto.randomUUID(),
    titulo: 'Refazer arte',
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
    ...mudancas,
})

const app = (rotas: Rotas = {}) =>
    comPerfil(['tarefa.ver'], {
        'GET /api/v1/tarefas': () => pagina([]),
        'GET /api/v1/equipe': () =>
            json([
                { id: perfil.usuarioId, nome: 'Ana Prado' },
                { id: RAFAEL, nome: 'Rafael Moura' },
            ]),
        ...rotas,
    })

const ultimaLista = (chamadas: Chamada[]) => chamadas.filter((c) => c.caminho === '/api/v1/tarefas').at(-1)

describe('tarefas', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('lista as em aberto com a origem e o prazo vencido em destaque', async () => {
        const chamadas = app({
            'GET /api/v1/tarefas': () =>
                pagina([
                    tarefa({ titulo: 'Trocar resistência', prazo: ontem, estimativaMinutos: 90, status: 'FAZENDO' }),
                    tarefa({
                        titulo: 'Plano de mídia',
                        clienteNome: 'Ótica Lux',
                        solicitacaoNumero: null,
                        projetoNome: 'Campanha de Natal',
                    }),
                    tarefa({ titulo: 'Revisar backups', clienteId: null, clienteNome: null, solicitacaoNumero: null }),
                ]),
        })
        await abrir('/tarefas')
        const linha = (titulo: string) => screen.getByText(titulo).closest('tr')!

        await screen.findByText('Trocar resistência')
        expect(within(linha('Trocar resistência')).getByText('Padaria Bom Grão · #1042')).toBeInTheDocument()
        expect(within(linha('Trocar resistência')).getByText('(atrasada)')).toHaveClass('sr-only')
        expect(within(linha('Trocar resistência')).getByText('Fazendo')).toBeInTheDocument()
        expect(within(linha('Plano de mídia')).getByText('Ótica Lux · Campanha de Natal')).toBeInTheDocument()
        expect(within(linha('Revisar backups')).getByText('Interna')).toBeInTheDocument()
        expect(ultimaLista(chamadas)?.consulta.get('status')).toBe('A_FAZER,FAZENDO')
        expect(screen.getByRole('link', { name: 'Tarefas' })).toHaveAttribute('aria-current', 'page')
    })

    it('aba, "sem responsável" e busca vão para o endereço e para a API', async () => {
        const chamadas = app()
        const roteador = await abrir('/tarefas')
        await screen.findByText('Nada em aberto')
        const usuario = userEvent.setup()

        await usuario.click(screen.getByRole('tab', { name: 'Concluídas' }))
        await usuario.type(screen.getByRole('combobox', { name: 'Responsável' }), 'sem')
        await usuario.click(await screen.findByRole('option', { name: 'Sem responsável' }))
        await usuario.type(screen.getByRole('searchbox', { name: 'Buscar tarefas' }), 'arte')

        await vi.waitFor(() =>
            expect(roteador.state.location.search).toEqual({
                aba: 'concluidas',
                responsavel: 'ninguem',
                busca: 'arte',
            }),
        )
        await vi.waitFor(() => {
            const consulta = ultimaLista(chamadas)?.consulta
            expect(consulta?.get('status')).toBe('CONCLUIDA')
            expect(consulta?.get('semResponsavel')).toBe('true')
            expect(consulta?.get('busca')).toBe('arte')
        })
    })

    it('"comigo" usa minhas=true', async () => {
        const chamadas = app()
        await abrir('/tarefas?responsavel=eu')
        await screen.findByText('Nenhuma tarefa com esses filtros')
        expect(ultimaLista(chamadas)?.consulta.get('minhas')).toBe('true')
        expect(ultimaLista(chamadas)?.consulta.has('responsavelId')).toBe(false)
    })

    it('troca para o quadro, com abertas e concluídas recentes numa chamada', async () => {
        const chamadas = app({
            'GET /api/v1/tarefas': () =>
                pagina([
                    tarefa({ titulo: 'Arte', status: 'A_FAZER' }),
                    tarefa({ titulo: 'Texto', status: 'FAZENDO' }),
                    tarefa({ titulo: 'Logo', status: 'CONCLUIDA', concluidaEm: new Date().toISOString() }),
                ]),
        })
        const roteador = await abrir('/tarefas')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('radio', { name: 'Quadro' }))

        await vi.waitFor(() => expect(roteador.state.location.search).toEqual({ modo: 'quadro' }))
        const coluna = (nome: string) => within(screen.getByRole('region', { name: new RegExp(`^${nome}`) }))
        expect(await coluna('A fazer').findByText('Arte')).toBeInTheDocument()
        expect(coluna('Fazendo').getByText('Texto')).toBeInTheDocument()
        expect(coluna('Concluída').getByText('Logo')).toBeInTheDocument()
        expect(screen.queryByRole('tab')).not.toBeInTheDocument()
        const consulta = ultimaLista(chamadas)?.consulta
        expect(consulta?.get('status')).toBe('A_FAZER,FAZENDO,CONCLUIDA')
        expect(new Date(consulta!.get('concluidaDesde')!).getTime()).toBeLessThan(Date.now() - 13 * 86_400_000)
    })

    it('mover para concluída passa por fazendo e o aviso desfaz', async () => {
        const arte = tarefa({ titulo: 'Arte', status: 'A_FAZER' })
        const resposta = (status: string) => () =>
            json({ ...arte, status, versao: '1', checklist: [], apontadoMinutos: 0 })
        const chamadas = comPerfil(['tarefa.ver', 'tarefa.editar'], {
            'GET /api/v1/tarefas': () => pagina([arte]),
            'GET /api/v1/equipe': () => json([]),
            [`POST /api/v1/tarefas/${arte.id}/inicio`]: resposta('FAZENDO'),
            [`POST /api/v1/tarefas/${arte.id}/conclusao`]: resposta('CONCLUIDA'),
            [`POST /api/v1/tarefas/${arte.id}/reabertura`]: resposta('FAZENDO'),
            [`POST /api/v1/tarefas/${arte.id}/retorno`]: resposta('A_FAZER'),
        })
        await abrir('/tarefas?modo=quadro')
        const usuario = userEvent.setup()

        await usuario.click(await screen.findByRole('button', { name: 'Mover Arte' }))
        await usuario.click(await screen.findByRole('menuitem', { name: 'Mover para concluída' }))
        expect(await screen.findByText('"Arte" em concluída.')).toBeInTheDocument()
        const transicoes = () => chamadas.filter((c) => c.metodo === 'POST' && c.caminho.startsWith('/api/v1/tarefas/'))
        expect(transicoes().map((c) => c.caminho.split('/').at(-1))).toEqual(['inicio', 'conclusao'])

        await usuario.click(screen.getByRole('button', { name: 'Desfazer' }))
        await vi.waitFor(() =>
            expect(transicoes().map((c) => c.caminho.split('/').at(-1))).toEqual([
                'inicio',
                'conclusao',
                'reabertura',
                'retorno',
            ]),
        )
    })
})
