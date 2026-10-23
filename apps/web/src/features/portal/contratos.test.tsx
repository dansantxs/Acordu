import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mesAtual, somarMeses } from '@/shared/format/datas'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comContato, json } from '@/testes/app'

const K = '0199f0a0-0000-7000-8000-0000000000e1'
const mes = mesAtual('America/Sao_Paulo')
const extrato = (periodo: string) => ({
    contrato: { tipo: 'MENSAL_FIXO', franquiaMinutos: 1200 },
    periodo,
    consumidoMinutos: 1035,
    saldoMinutos: 165,
    percentual: 86,
    itens: [
        {
            data: `${periodo}-14`,
            solicitacaoNumero: 1042,
            solicitacaoTitulo: 'Ajustar banner',
            descricao: 'Arte refeita',
            duracaoMinutos: 90,
        },
        {
            data: `${periodo}-15`,
            solicitacaoNumero: null,
            solicitacaoTitulo: null,
            descricao: 'Atendimento',
            duracaoMinutos: 30,
        },
    ],
})

describe('portal: contratos', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('mostra o extrato do mês na régua e volta um mês pelo endereço', async () => {
        const chamadas = comContato({
            '/api/v1/portal/solicitacoes': () => json({ itens: [], pagina: 1, tamanhoPagina: 20, total: 0 }),
            '/api/v1/portal/contratos': () =>
                json([
                    {
                        id: K,
                        tipo: 'MENSAL_FIXO',
                        inicio: `${somarMeses(mes, -6)}-01`,
                        fim: null,
                        franquiaMinutos: 1200,
                    },
                ]),
            [`/api/v1/portal/contratos/${K}/extrato`]: (_, url) => json(extrato(url.searchParams.get('periodo')!)),
        })
        const roteador = await abrir('/portal')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('link', { name: 'Contratos' }))
        const secao = await screen.findByRole('region', { name: /Mensal fixo/ })
        expect(await within(secao).findByText('restam 2h45', { exact: false })).toBeInTheDocument()
        expect(within(secao).getByRole('meter', { name: 'Consumo do contrato' })).toHaveAttribute('aria-valuenow', '86')
        expect(within(secao).getByText('Arte refeita')).toBeInTheDocument()
        expect(within(secao).getByText('#1042 Ajustar banner')).toBeInTheDocument()
        expect(within(secao).getByRole('button', { name: 'Mês seguinte' })).toBeDisabled()

        await usuario.click(within(secao).getByRole('button', { name: 'Mês anterior' }))
        await vi.waitFor(() => expect(roteador.state.location.search).toEqual({ mes: somarMeses(mes, -1) }))
        await vi.waitFor(() =>
            expect(
                chamadas
                    .filter((c) => c.caminho.endsWith('/extrato'))
                    .at(-1)
                    ?.consulta.get('periodo'),
            ).toBe(somarMeses(mes, -1)),
        )
    })

    it('escopo "só as próprias" não tem Contratos e o endereço volta para a lista', async () => {
        comContato(
            { '/api/v1/portal/solicitacoes': () => json({ itens: [], pagina: 1, tamanhoPagina: 20, total: 0 }) },
            'PROPRIAS',
        )
        const roteador = await abrir('/portal/contratos')
        await vi.waitFor(() => expect(roteador.state.location.pathname).toBe('/portal'))
        expect(screen.queryByRole('link', { name: 'Contratos' })).not.toBeInTheDocument()
    })
})
