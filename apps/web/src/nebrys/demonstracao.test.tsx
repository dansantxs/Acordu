import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrirSessaoOperacao, reiniciarSessaoOperacaoParaTestes, sessaoOperacao } from '@/shared/sessao/sessaoOperacao'
import { abrir, daquiA, json, semSessao, servidor, type Rotas } from '@/testes/app'

// Spec do F14, empresa de demonstração na área (F14s: F14-87 a F14-90, F14-110), sobre a API do contrato §17.

const TOKEN = 'token-de-operacao'
const ROTA = '/api/v1/nebrys/demonstracao'

const ana = { usuarioId: '0193d0d0-0000-7000-8000-000000000001', nome: 'Ana Souza', email: 'ana@estudiotraco.com.br' }

const semDemonstracao = { empresaId: null, nome: null, criadaEm: null, indicadas: [] }
const criada = (indicadas: (typeof ana)[] = []) => ({
    empresaId: '0193d0d0-0000-7000-8000-0000000000aa',
    nome: 'Estúdio Aurora',
    criadaEm: '2026-11-24T13:00:00Z',
    indicadas,
})

const problema = (status: number, tipo: string, detail: string, errors?: Record<string, string[]>) =>
    new Response(JSON.stringify({ type: `https://acordu.app.br/erros/${tipo}`, status, title: tipo, detail, errors }), {
        status,
        headers: { 'content-type': 'application/problem+json' },
    })

function operando(rotas: Rotas = {}) {
    abrirSessaoOperacao({ accessToken: TOKEN, expiraEm: daquiA(30 * 60_000) })
    return servidor({ '/api/v1/auth/refresh': semSessao, ...rotas })
}

describe('área da Nebrys: empresa de demonstração', () => {
    beforeEach(() => {
        reiniciarParaTestes()
        reiniciarSessaoOperacaoParaTestes()
        localStorage.clear()
        sessionStorage.clear()
    })
    afterEach(() => vi.unstubAllGlobals())

    it('Casca_ComSessao_TemDemonstracao', async () => {
        operando({ [ROTA]: () => json(semDemonstracao) })
        await abrir('/nebrys/demonstracao')
        const menu = await screen.findByRole('navigation', { name: 'Área da Nebrys' })
        expect(within(menu).getByRole('link', { name: 'Demonstração' })).toHaveAttribute('href', '/nebrys/demonstracao')
    })

    it('Demonstracao_SemSessaoDeOperacao_VaiParaAEntradaSemChamarAArea', async () => {
        const chamadas = servidor({ '/api/v1/auth/refresh': semSessao })
        const roteador = await abrir('/nebrys/demonstracao')
        expect(await screen.findByRole('heading', { name: 'Área da Nebrys' })).toBeInTheDocument()
        expect(roteador.state.location.pathname).toBe('/nebrys')
        expect(chamadas.some((c) => c.caminho.startsWith('/api/v1/nebrys'))).toBe(false)
    })

    it('Demonstracao_LidaRecusadaPelaBarreira_EncerraAOperacao', async () => {
        operando({ [ROTA]: () => problema(404, 'nao-encontrado', 'Não encontrado.') })
        const roteador = await abrir('/nebrys/demonstracao')
        await waitFor(() => expect(roteador.state.location.pathname).toBe('/nebrys'))
        expect(sessaoOperacao()).toBeNull()
    })

    it('Criar_SemDemonstracao_ChamaACriacaoEMostraAEmpresa', async () => {
        // F14-87
        const chamadas = operando({
            [`GET ${ROTA}`]: () => json(semDemonstracao),
            [`POST ${ROTA}`]: () => json(criada(), 201),
        })
        const usuario = userEvent.setup()
        await abrir('/nebrys/demonstracao')

        await usuario.click(await screen.findByRole('button', { name: 'Criar a empresa de demonstração' }))

        expect(await screen.findByText('Estúdio Aurora')).toBeInTheDocument()
        expect(screen.getByText('Criada em 24/11/2026')).toBeInTheDocument()
        expect(screen.getByRole('link', { name: 'Ver o detalhe da empresa' })).toHaveAttribute(
            'href',
            `/nebrys/empresas/${criada().empresaId}`,
        )
        expect(chamadas.filter((c) => c.metodo === 'POST' && c.caminho === ROTA)).toHaveLength(1)
        const autorizacao = vi
            .mocked(globalThis.fetch)
            .mock.calls.map(([r]) => r as Request)
            .find((r) => r.method === 'POST' && new URL(r.url).pathname === ROTA)
            ?.headers.get('Authorization')
        expect(autorizacao).toBe(`Bearer ${TOKEN}`)
    })

    it('Demonstracao_Criada_MostraQuemFoiIndicado', async () => {
        operando({ [ROTA]: () => json(criada([ana])) })
        await abrir('/nebrys/demonstracao')

        expect(await screen.findByText('Ana Souza')).toBeInTheDocument()
        expect(screen.getByText(ana.email)).toBeInTheDocument()
        expect(screen.queryByRole('button', { name: 'Criar a empresa de demonstração' })).not.toBeInTheDocument()
    })

    it('Indicar_ContaQueExiste_IndicaEMostraNaLista', async () => {
        // F14-90
        const chamadas = operando({
            [`GET ${ROTA}`]: () => json(criada()),
            [`POST ${ROTA}/indicacoes`]: () => json(criada([ana])),
        })
        const usuario = userEvent.setup()
        await abrir('/nebrys/demonstracao')

        await usuario.type(await screen.findByLabelText('E-mail da conta'), ana.email)
        await usuario.click(screen.getByRole('button', { name: 'Indicar' }))

        expect(await screen.findByText(`${ana.email} entra na demonstração como Dono.`)).toBeInTheDocument()
        expect(chamadas.find((c) => c.metodo === 'POST' && c.caminho === `${ROTA}/indicacoes`)?.corpo).toEqual({
            email: ana.email,
        })
        expect(screen.getByText('Ana Souza')).toBeInTheDocument()
        expect(screen.getByLabelText('E-mail da conta')).toHaveValue('')
    })

    it('Indicar_EmailSemConta400_MostraAExplicacaoNoCampo', async () => {
        // F14-110
        const texto = 'Este e-mail não tem conta no Acordu. A demonstração não manda convite.'
        operando({
            [`GET ${ROTA}`]: () => json(criada()),
            [`POST ${ROTA}/indicacoes`]: () => problema(400, 'validacao', 'Dados inválidos.', { email: [texto] }),
        })
        const usuario = userEvent.setup()
        await abrir('/nebrys/demonstracao')

        await usuario.type(await screen.findByLabelText('E-mail da conta'), 'sem-conta@exemplo.com')
        await usuario.click(screen.getByRole('button', { name: 'Indicar' }))

        expect(await screen.findByText(texto)).toBeInTheDocument()
        expect(screen.getByLabelText('E-mail da conta')).toHaveAttribute('aria-invalid', 'true')
        expect(screen.getByText('Ninguém indicado ainda')).toBeInTheDocument()
    })

    it('Indicar_JaIndicada422_MostraAExplicacao', async () => {
        const texto = 'Esta pessoa já está na empresa de demonstração.'
        operando({
            [`GET ${ROTA}`]: () => json(criada([ana])),
            [`POST ${ROTA}/indicacoes`]: () => problema(422, 'transicao-invalida', texto),
        })
        const usuario = userEvent.setup()
        await abrir('/nebrys/demonstracao')

        await usuario.type(await screen.findByLabelText('E-mail da conta'), ana.email)
        await usuario.click(screen.getByRole('button', { name: 'Indicar' }))

        expect(await screen.findByRole('alert')).toHaveTextContent(texto)
    })

    it('Restaurar_Confirmado_ChamaARestauracaoEAvisa', async () => {
        // F14-88
        const chamadas = operando({
            [`GET ${ROTA}`]: () => json(criada([ana])),
            [`POST ${ROTA}/restauracao`]: () => json(criada([ana])),
        })
        const usuario = userEvent.setup()
        await abrir('/nebrys/demonstracao')

        await usuario.click(await screen.findByRole('button', { name: 'Restaurar os dados' }))
        const janela = screen.getByRole('dialog')
        expect(janela).toHaveTextContent(/as pessoas indicadas continuam/i)
        expect(janela).toHaveTextContent(/Nenhuma outra empresa é tocada/)
        await usuario.click(within(janela).getByRole('button', { name: 'Restaurar' }))

        expect(await screen.findByText('Dados da demonstração restaurados.')).toBeInTheDocument()
        expect(chamadas.filter((c) => c.metodo === 'POST' && c.caminho === `${ROTA}/restauracao`)).toHaveLength(1)
        expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    })

    it('Restaurar_DemonstracaoSumiu404_ReleATelaEAvisa', async () => {
        let lida = 0
        operando({
            [`GET ${ROTA}`]: () => {
                lida++
                return json(lida === 1 ? criada() : semDemonstracao)
            },
            [`POST ${ROTA}/restauracao`]: () =>
                problema(404, 'nao-encontrado', 'A empresa de demonstração ainda não foi criada.'),
        })
        const usuario = userEvent.setup()
        await abrir('/nebrys/demonstracao')

        await usuario.click(await screen.findByRole('button', { name: 'Restaurar os dados' }))
        await usuario.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Restaurar' }))

        expect(await screen.findByText(/não existe mais/)).toBeInTheDocument()
        expect(await screen.findByRole('button', { name: 'Criar a empresa de demonstração' })).toBeInTheDocument()
        expect(sessaoOperacao()).not.toBeNull()
    })

    it('Tirar_Confirmado_ApagaAIndicacaoEReleATela', async () => {
        let lida = 0
        const chamadas = operando({
            [`GET ${ROTA}`]: () => {
                lida++
                return json(lida === 1 ? criada([ana]) : criada())
            },
            [`DELETE ${ROTA}/indicacoes/${ana.usuarioId}`]: () => new Response(null, { status: 204 }),
        })
        const usuario = userEvent.setup()
        await abrir('/nebrys/demonstracao')

        await usuario.click(await screen.findByRole('button', { name: `Tirar ${ana.nome} da demonstração` }))
        await usuario.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Tirar da demonstração' }))

        expect(await screen.findByText(`${ana.nome} saiu da demonstração.`)).toBeInTheDocument()
        expect(chamadas.some((c) => c.metodo === 'DELETE')).toBe(true)
        expect(await screen.findByText('Ninguém indicado ainda')).toBeInTheDocument()
    })
})
