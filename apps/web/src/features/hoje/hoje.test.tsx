import { screen, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { hojeNoFuso, segundaDaSemana, somarDias } from '@/shared/format/datas'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, json, type Rotas } from '@/testes/app'

const hoje = hojeNoFuso('America/Sao_Paulo')
const pagina = (itens: unknown[]) => json({ itens, pagina: 1, tamanhoPagina: 20, total: itens.length })
const solicitacao = (id: string, mudancas: Record<string, unknown> = {}) => ({
    id,
    numero: 12,
    titulo: 'Impressora parou',
    cliente: { id: 'c1', nome: 'Padaria Bom Grão' },
    prioridade: 'ALTA',
    status: 'EM_ANDAMENTO',
    statusExterno: 'EM_ANDAMENTO',
    responsavel: { id: 'u1', nome: 'Ana Prado' },
    abertaEm: '2026-10-20T12:00:00Z',
    prazoResolucao: null,
    situacaoSla: 'EM_RISCO',
    ...mudancas,
})
const tarefa = (id: string, titulo: string, mudancas: Record<string, unknown> = {}) => ({
    id,
    titulo,
    status: 'A_FAZER',
    clienteId: null,
    solicitacaoId: null,
    projetoId: null,
    responsavel: { id: 'u1', nome: 'Ana Prado' },
    prazo: null,
    estimativaMinutos: null,
    visivelCliente: false,
    planejadaPara: null,
    ...mudancas,
})

const rotas: Rotas = {
    'GET /api/v1/solicitacoes': (_, url) =>
        url.searchParams.get('status')?.startsWith('AGUARDANDO')
            ? pagina([solicitacao('s2', { numero: 13, titulo: 'Logo nova', status: 'AGUARDANDO_CLIENTE' })])
            : pagina([solicitacao('s1')]),
    'GET /api/v1/tarefas': () =>
        pagina([
            tarefa('t1', 'Trocar cabo', { prazo: somarDias(hoje, -2) }),
            tarefa('t2', 'Revisar texto', { planejadaPara: hoje }),
            tarefa('t3', 'Arte do mês que vem', { prazo: somarDias(hoje, 20) }),
        ]),
    'GET /api/v1/capacidade': () =>
        json({
            semana: segundaDaSemana(hoje),
            ate: somarDias(segundaDaSemana(hoje), 6),
            pessoas: [
                {
                    pessoa: { id: 'u1', nome: 'Ana Prado' },
                    capacidadeMinutos: 2400,
                    percentual: 110,
                    carga: {
                        planejadoMinutos: 2640,
                        tarefas: 3,
                        tarefasSemEstimativa: 1,
                        solicitacoes: 1,
                        porDia: [0, 1, 2, 3, 4, 5, 6].map((i) => ({
                            data: somarDias(segundaDaSemana(hoje), i),
                            planejadoMinutos: 480,
                        })),
                    },
                },
            ],
            semResponsavel: { planejadoMinutos: 0, tarefas: 0, tarefasSemEstimativa: 0, solicitacoes: 0, porDia: [] },
        }),
    'GET /api/v1/apontamentos': () =>
        json({
            itens: [{ id: 'a1', tarefa: { id: 't1', nome: 'Trocar cabo' }, duracaoMinutos: 90, data: hoje }],
            pagina: 1,
            tamanhoPagina: 200,
            total: 1,
        }),
}

describe('hoje', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('junta o que está na mão, o que espera, a semana e as horas de hoje', async () => {
        const chamadas = comPerfil(['solicitacao.ver', 'tarefa.ver'], rotas)
        await abrir('/')
        const naMao = await screen.findByRole('region', { name: /Solicitações com você/ })
        expect(await within(naMao).findByRole('link', { name: /Impressora parou/ })).toBeInTheDocument()
        const pedido = chamadas.find((c) => c.caminho === '/api/v1/solicitacoes')!.consulta
        expect([pedido.get('responsavelId'), pedido.get('ordem')]).toEqual(['u1', 'prazoResolucao'])

        const tarefas = screen.getByRole('region', { name: /Tarefas de hoje/ })
        expect(await within(tarefas).findByText(/atrasada desde/)).toBeInTheDocument()
        expect(within(tarefas).getByRole('link', { name: 'Revisar texto' })).toBeInTheDocument()
        expect(within(tarefas).queryByText('Arte do mês que vem')).not.toBeInTheDocument()
        expect(within(tarefas).getByRole('link', { name: 'Mais 1 adiante' })).toBeInTheDocument()

        expect(await screen.findByRole('link', { name: /Logo nova/ })).toBeInTheDocument()
        expect(await screen.findByText('110%', { exact: false })).toBeInTheDocument()
        const horas = screen.getByRole('region', { name: /Apontado hoje/ })
        expect(await within(horas).findByText(/em 1 apontamento/)).toBeInTheDocument()
    })

    it('sem permissão de solicitações e tarefas, só as horas e as novidades', async () => {
        comPerfil([], rotas)
        await abrir('/')
        expect(await screen.findByRole('region', { name: /Apontado hoje/ })).toBeInTheDocument()
        expect(screen.queryByRole('region', { name: /Solicitações com você/ })).not.toBeInTheDocument()
        expect(screen.queryByRole('region', { name: /Sua semana/ })).not.toBeInTheDocument()
    })
})
