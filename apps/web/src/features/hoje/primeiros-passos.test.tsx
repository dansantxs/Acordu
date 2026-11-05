import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, json, type Rotas } from '@/testes/app'

const pagina = (total: number) => json({ itens: [], pagina: 1, tamanhoPagina: 1, total })
const DONO = ['configuracao.gerenciar', 'usuario.convidar', 'cliente.gerenciar', 'contrato.gerenciar']

/** Empresa nova: só o calendário com feriados e um cliente cadastrado. */
const empresaNova = (mudancas: Rotas = {}): Rotas => ({
    'GET /api/v1/configuracoes/empresa': () =>
        json({ nome: 'Prado', responderPara: null, slaPadrao: [], slaPersonalizado: false }),
    'GET /api/v1/configuracoes/calendario': () =>
        json({ fusoHorario: 'America/Sao_Paulo', expediente: [], feriados: ['2026-11-02'] }),
    'GET /api/v1/usuarios': () => json([{ usuarioId: 'u1' }]),
    'GET /api/v1/convites': () => json([]),
    'GET /api/v1/clientes': () => pagina(1),
    'GET /api/v1/contratos': () => pagina(0),
    ...mudancas,
})

describe('hoje: primeiros passos', () => {
    beforeEach(() => {
        reiniciarParaTestes()
        localStorage.clear()
    })
    afterEach(() => vi.unstubAllGlobals())

    it('mostra o progresso pelas listas, com link para o que falta', async () => {
        comPerfil(DONO, empresaNova())
        await abrir('/')
        const cartao = await screen.findByRole('region', { name: 'Primeiros passos' })
        expect(within(cartao).getByRole('meter', { name: 'Primeiros passos feitos' })).toHaveAttribute(
            'aria-valuenow',
            '2',
        )
        expect(within(cartao).getByText('2 de 5')).toBeInTheDocument()
        expect(within(cartao).getByText(/Cadastre os clientes/)).toHaveTextContent('(feito)')
        expect(within(cartao).getByRole('link', { name: 'Crie os contratos' })).toHaveAttribute('href', '/contratos')
    })

    it('"Dispensar" esconde o cartão e lembra neste navegador', async () => {
        comPerfil(DONO, empresaNova())
        await abrir('/')
        const cartao = await screen.findByRole('region', { name: 'Primeiros passos' })
        await userEvent.setup().click(within(cartao).getByRole('button', { name: 'Dispensar' }))
        expect(screen.queryByRole('region', { name: 'Primeiros passos' })).not.toBeInTheDocument()
        expect(localStorage.getItem('acordu.primeiros-passos.t1')).toBe('dispensado')
    })

    it('some quando tudo foi feito', async () => {
        comPerfil(
            DONO,
            empresaNova({
                'GET /api/v1/configuracoes/empresa': () =>
                    json({ nome: 'Prado', responderPara: 'oi@prado.com', slaPadrao: [], slaPersonalizado: false }),
                'GET /api/v1/convites': () => json([{ id: 'c1' }]),
                'GET /api/v1/contratos': () => pagina(2),
            }),
        )
        await abrir('/')
        expect(await screen.findByRole('region', { name: /Apontado hoje/ })).toBeInTheDocument()
        await new Promise((r) => setTimeout(r, 50))
        expect(screen.queryByRole('region', { name: 'Primeiros passos' })).not.toBeInTheDocument()
    })

    it('quem não configura a empresa não vê o cartão', async () => {
        comPerfil(['cliente.gerenciar'], empresaNova())
        await abrir('/')
        expect(await screen.findByRole('region', { name: /Apontado hoje/ })).toBeInTheDocument()
        expect(screen.queryByRole('region', { name: 'Primeiros passos' })).not.toBeInTheDocument()
    })

    it('voltar às configurações depois do Hoje abre a seção normalmente (chave própria da verificação)', async () => {
        comPerfil(DONO, empresaNova())
        const roteador = await abrir('/')
        await screen.findByRole('region', { name: 'Primeiros passos' })
        await roteador.navigate({ to: '/configuracoes/empresa' })
        expect(await screen.findByRole('textbox', { name: /Responder para/ })).toBeInTheDocument()
        expect(screen.queryByText(/Sem conexão com o servidor/)).not.toBeInTheDocument()
        await roteador.navigate({ to: '/configuracoes/calendario' })
        expect(await screen.findByText(/O SLA conta só as horas do expediente/)).toBeInTheDocument()
        expect(screen.queryByText(/Sem conexão com o servidor/)).not.toBeInTheDocument()
    })

    it('salvar a empresa marca o passo como feito ao voltar ao Hoje', async () => {
        const slaPadrao = ['URGENTE', 'ALTA', 'MEDIA', 'BAIXA'].map((prioridade) => ({
            prioridade,
            primeiraRespostaMinutos: 60,
            resolucaoMinutos: 240,
        }))
        let empresa = { nome: 'Prado', responderPara: null as string | null, slaPadrao, slaPersonalizado: false }
        comPerfil(
            DONO,
            empresaNova({
                'GET /api/v1/configuracoes/empresa': () => json(empresa),
                'PUT /api/v1/configuracoes/empresa': (corpo) => {
                    empresa = { ...empresa, ...(corpo as object) }
                    return json(empresa)
                },
            }),
        )
        const roteador = await abrir('/')
        const cartao = await screen.findByRole('region', { name: 'Primeiros passos' })
        expect(within(cartao).getByText(/Configure a empresa/)).not.toHaveTextContent('(feito)')

        await roteador.navigate({ to: '/configuracoes/empresa' })
        const usuario = userEvent.setup()
        await usuario.type(await screen.findByRole('textbox', { name: /Responder para/ }), 'oi@prado.com.br')
        await usuario.click(screen.getByRole('button', { name: 'Salvar' }))
        await screen.findByText('Dados da empresa salvos.')

        await roteador.navigate({ to: '/' })
        const depois = await screen.findByRole('region', { name: 'Primeiros passos' })
        await vi.waitFor(() => expect(within(depois).getByText(/Configure a empresa/)).toHaveTextContent('(feito)'))
    })
})
