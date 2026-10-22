import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { fimDoMes, mesAtual, somarMeses } from '@/shared/format/datas'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, json } from '@/testes/app'

const mes = mesAtual('America/Sao_Paulo')
const anterior = somarMeses(mes, -1)
const K = '0199f0a0-0000-7000-8000-0000000000e1'
const C = '0199f0a0-0000-7000-8000-0000000000c1'
const painel = (de: string, ate: string, comHoras = true) => ({
    de,
    ate,
    fila: { abertas: 14, emAndamento: 9, aguardandoCliente: 4, aguardandoTerceiro: 1, semResponsavel: 2 },
    sla: { emRisco: 3, violadas: 0, cumprimentoPeriodo: 93 },
    horas: comHoras
        ? {
              faturavelMinutos: 9120,
              naoFaturavelMinutos: 1340,
              porCliente: [{ clienteId: C, nome: 'Padaria Bom Grão', minutos: 1035 }],
          }
        : null,
    contratos: [
        {
            contratoId: K,
            cliente: 'Padaria Bom Grão',
            tipo: 'MENSAL_FIXO',
            franquiaMinutos: 1200,
            consumidoMinutos: 1035,
            percentual: 86,
        },
    ],
    tarefas: { vencidas: 2, proximasRecorrentes: null },
})

describe('panorama', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('mostra os blocos do período, com atalhos para a fila e a régua do contrato', async () => {
        const chamadas = comPerfil(['dashboard.ver'], {
            'GET /api/v1/dashboard': (_, url) =>
                json(painel(url.searchParams.get('de')!, url.searchParams.get('ate')!)),
        })
        const roteador = await abrir('/panorama')
        const fila = await screen.findByRole('region', { name: 'Fila agora' })
        expect(within(fila).getByRole('link', { name: /Sem responsável/ })).toHaveAttribute(
            'href',
            '/solicitacoes?responsavel=ninguem',
        )
        expect(screen.getByText('93%')).toBeInTheDocument()
        expect(screen.getByText(/174h00|174h/)).toBeInTheDocument()
        expect(screen.getByRole('meter', { name: 'Consumo de Padaria Bom Grão' })).toHaveAttribute(
            'aria-valuenow',
            '86',
        )
        expect(chamadas.find((c) => c.caminho === '/api/v1/dashboard')?.consulta.get('de')).toBe(`${mes}-01`)

        await userEvent.setup().click(screen.getByRole('tab', { name: 'Mês passado' }))
        await vi.waitFor(() => expect(roteador.state.location.search).toEqual({ periodo: 'anterior' }))
        await vi.waitFor(() =>
            expect(
                chamadas
                    .filter((c) => c.caminho === '/api/v1/dashboard')
                    .at(-1)
                    ?.consulta.get('ate'),
            ).toBe(fimDoMes(anterior)),
        )
    })

    it('bloco sem permissão vem nulo e some', async () => {
        comPerfil(['dashboard.ver'], {
            'GET /api/v1/dashboard': (_, url) =>
                json(painel(url.searchParams.get('de')!, url.searchParams.get('ate')!, false)),
        })
        await abrir('/panorama')
        expect(await screen.findByRole('region', { name: 'Fila agora' })).toBeInTheDocument()
        expect(screen.queryByRole('region', { name: 'Horas no período' })).not.toBeInTheDocument()
    })
})
