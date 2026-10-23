import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { hojeNoFuso, segundaDaSemana, somarDias } from '@/shared/format/datas'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, json } from '@/testes/app'
import type { Agenda, ItemAgenda } from './consultas'
import { colunasDaJanela, montarFaixas } from './LinhaDoTempo'

const seg = segundaDaSemana(hojeNoFuso('America/Sao_Paulo'))
const dia = (n: number) => somarDias(seg, n)
const item = (mudancas: Partial<ItemAgenda>): ItemAgenda => ({
    tipo: 'TAREFA',
    id: Math.random().toString(36).slice(2),
    titulo: 'Item',
    inicio: dia(1),
    fim: dia(1),
    situacao: 'NO_PRAZO',
    responsavel: null,
    cliente: null,
    ...mudancas,
})
const ANA = { id: 'u1', nome: 'Ana Prado' }
const RAFAEL = { id: 'u2', nome: 'Rafael Moura' }
const PADARIA = { id: 'c1', nome: 'Padaria Bom Grão' }
const agenda = (itens: ItemAgenda[]): Agenda => ({
    de: seg,
    ate: dia(13),
    hoje: dia(2),
    dias: Array.from({ length: 14 }, (_, i) => ({ data: dia(i), util: i % 7 < 5 && i !== 3, feriado: i === 3 })),
    equipe: [ANA, RAFAEL],
    itens,
})

const itens = [
    item({
        tipo: 'SOLICITACAO',
        titulo: 'Banner',
        numero: 128,
        situacao: 'EM_RISCO',
        responsavel: ANA,
        cliente: PADARIA,
    }),
    item({
        titulo: 'Arte do cardápio',
        inicio: dia(2),
        fim: dia(2),
        responsavel: RAFAEL,
        cliente: PADARIA,
        estimativaMinutos: 120,
    }),
    item({
        titulo: 'Revisão atrasada',
        inicio: somarDias(seg, -3),
        fim: somarDias(seg, -3),
        situacao: 'VENCIDO',
        responsavel: ANA,
    }),
    item({ tipo: 'PROJETO', titulo: 'Site novo', inicio: dia(1), fim: dia(8), cliente: PADARIA }),
    item({
        tipo: 'RECORRENCIA',
        titulo: 'Fechamento',
        inicio: dia(5),
        fim: dia(5),
        situacao: 'PREVISTO',
        responsavel: RAFAEL,
    }),
    item({
        tipo: 'CONTRATO',
        titulo: 'Contrato',
        marco: 'CONSUMO_ALTO',
        percentual: 86,
        situacao: 'EM_RISCO',
        cliente: PADARIA,
    }),
]

describe('agenda: linha do tempo', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('faixas por pessoa com projetos e contratos no topo; por cliente agrupa pelo cliente', () => {
        const porPessoa = montarFaixas(agenda(itens), 'pessoa')
        expect(porPessoa.map((f) => f.nome)).toEqual(['Projetos e contratos', 'Ana Prado', 'Rafael Moura'])
        expect(porPessoa[1]!.itens.map((i) => i.titulo)).toEqual(['Banner', 'Revisão atrasada'])
        const porCliente = montarFaixas(agenda(itens), 'cliente')
        expect(porCliente.map((f) => f.nome)).toEqual(['Padaria Bom Grão', 'Sem cliente'])
        expect(colunasDaJanela(agenda(itens))).toHaveLength(10)
    })

    it('mostra a janela com atrasados, feriado e links; navega e troca o agrupamento pelo endereço', async () => {
        const chamadas = comPerfil(['tarefa.ver', 'solicitacao.ver'], {
            'GET /api/v1/agenda': () => json(agenda(itens)),
            'GET /api/v1/capacidade': (_, url) =>
                json({
                    semana: url.searchParams.get('semana'),
                    ate: url.searchParams.get('semana'),
                    pessoas: [
                        {
                            pessoa: ANA,
                            capacidadeMinutos: 2400,
                            percentual: url.searchParams.get('semana') === seg ? 77 : 40,
                            carga: {
                                planejadoMinutos: 0,
                                tarefas: 0,
                                tarefasSemEstimativa: 0,
                                solicitacoes: 0,
                                porDia: [],
                            },
                        },
                    ],
                    semResponsavel: {
                        planejadoMinutos: 0,
                        tarefas: 0,
                        tarefasSemEstimativa: 0,
                        solicitacoes: 0,
                        porDia: [],
                    },
                }),
        })
        const roteador = await abrir('/agenda')
        const tabela = await screen.findByRole('table', { name: 'Linha do tempo' })
        const ana = within(tabela).getByRole('row', { name: 'Ana Prado' })
        expect(within(ana).getByText('77% · 40% da capacidade')).toBeInTheDocument()
        expect(within(ana).getByRole('link', { name: /#128 Banner/ })).toHaveAttribute(
            'href',
            expect.stringContaining('/solicitacoes/'),
        )
        expect(within(ana).getByRole('link', { name: /Revisão atrasada/ })).toBeInTheDocument()
        expect(within(tabela).getByRole('link', { name: /Site novo/ })).toHaveAttribute(
            'href',
            expect.stringContaining('/projetos/'),
        )
        expect(within(tabela).getByText('feriado')).toBeInTheDocument()
        expect(chamadas.find((c) => c.caminho === '/api/v1/agenda')?.consulta.get('ate')).toBe(dia(13))

        const usuario = userEvent.setup()
        await usuario.click(screen.getByRole('button', { name: 'Semana seguinte' }))
        await vi.waitFor(() => expect(roteador.state.location.search).toEqual({ inicio: dia(7) }))
        await usuario.click(screen.getByRole('tab', { name: 'Por cliente' }))
        await vi.waitFor(() => expect(roteador.state.location.search).toEqual({ inicio: dia(7), faixas: 'cliente' }))
        expect(
            await within(screen.getByRole('table', { name: 'Linha do tempo' })).findByRole('row', {
                name: 'Padaria Bom Grão',
            }),
        ).toBeInTheDocument()
        await usuario.click(screen.getByRole('button', { name: 'Hoje' }))
        await vi.waitFor(() => expect(roteador.state.location.search).toEqual({ faixas: 'cliente' }))
    })

    it('a lista por dia (celular) traz os atrasados primeiro e o dia de hoje marcado', async () => {
        comPerfil(['solicitacao.ver'], { 'GET /api/v1/agenda': () => json(agenda(itens)) })
        await abrir('/agenda')
        const lista = await screen.findByRole('list', { name: 'Agenda por dia' })
        const titulos = within(lista)
            .getAllByRole('heading')
            .map((h) => h.textContent)
        expect(titulos[0]).toBe('Atrasados')
        expect(titulos.some((t) => t?.includes('(hoje)'))).toBe(true)
    })
})
