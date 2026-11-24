import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Tenant } from '@/shared/sessao/gravacao'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comSessao, json, perfil, servidor, type Rotas } from '@/testes/app'

/** Dados de exemplo no Hoje (spec do F14: F14-91 a F14-95; RF-EMP-05). */

const DONO = ['configuracao.gerenciar']
const vazio = () => json({ itens: [], pagina: 1, tamanhoPagina: 1, total: 0 })

/** Sessão da Ana com as permissões e a empresa dadas; o estado dos exemplos vem de `exemplos()`. */
function app(
    permissoes: string[],
    exemplos: () => { disponivel: boolean; carregados: boolean },
    rotas: Rotas = {},
    tenant: Partial<Tenant> = {},
) {
    return servidor({
        '/api/v1/auth/refresh': comSessao,
        '/api/v1/auth/eu': () => json({ ...perfil, permissoes, tenant: { ...perfil.tenant, ...tenant } }),
        '/api/v1/notificacoes': () => json({ itens: [], pagina: 1, tamanhoPagina: 10, total: 0, naoLidas: 0 }),
        'GET /api/v1/apontamentos': vazio,
        'GET /api/v1/configuracoes/empresa': () =>
            json({ nome: 'Prado', responderPara: null, slaPadrao: [], slaPersonalizado: false }),
        'GET /api/v1/configuracoes/calendario': () =>
            json({ fusoHorario: 'America/Sao_Paulo', expediente: [], feriados: [] }),
        'GET /api/v1/configuracoes/exemplos': () => json(exemplos()),
        ...rotas,
    })
}

const cartao = () => screen.findByRole('region', { name: /Quer ver o Acordu com dados\?|Dados de exemplo/ })

describe('hoje: dados de exemplo', () => {
    beforeEach(() => {
        reiniciarParaTestes()
        localStorage.clear()
    })
    afterEach(() => vi.unstubAllGlobals())

    it('DadosDeExemplo_EmpresaEmTesteSemDados_OfereceCarregar (F14-91)', async () => {
        app(DONO, () => ({ disponivel: true, carregados: false }))
        await abrir('/')
        const regiao = await cartao()
        expect(within(regiao).getByRole('heading', { name: 'Quer ver o Acordu com dados?' })).toBeInTheDocument()
        expect(within(regiao).getByText(/Nenhum e-mail é enviado/)).toBeInTheDocument()
        expect(within(regiao).getByRole('button', { name: 'Carregar dados de exemplo' })).toBeEnabled()
    })

    it('Carregar_PeloCartao_ChamaAApiRelendoTudoEAvisa (F14-92)', async () => {
        let carregados = false
        const chamadas = app(DONO, () => ({ disponivel: !carregados, carregados }), {
            'POST /api/v1/configuracoes/exemplos': () => {
                carregados = true
                return new Response(null, { status: 204 })
            },
        })
        await abrir('/')
        const regiao = await cartao()
        await userEvent.setup().click(within(regiao).getByRole('button', { name: 'Carregar dados de exemplo' }))

        expect(await screen.findByText(/Os dados de exemplo foram carregados/)).toBeInTheDocument()
        expect(
            chamadas.filter((c) => c.metodo === 'POST' && c.caminho === '/api/v1/configuracoes/exemplos'),
        ).toHaveLength(1)
        expect(await screen.findByRole('button', { name: 'Apagar dados de exemplo' })).toBeInTheDocument()
    })

    it('DadosDeExemplo_EmpresaComDadosProprios_NaoOferece (F14-93)', async () => {
        const chamadas = app(DONO, () => ({ disponivel: false, carregados: false }))
        await abrir('/')
        expect(await screen.findByRole('region', { name: /Apontado hoje/ })).toBeInTheDocument()
        await vi.waitFor(() => expect(chamadas.some((c) => c.caminho === '/api/v1/configuracoes/exemplos')).toBe(true))
        expect(screen.queryByRole('region', { name: /Quer ver o Acordu com dados\?|Dados de exemplo/ })).toBeNull()
    })

    it('Carregar_ApiRecusa_MostraAExplicacao (F14-93)', async () => {
        app(DONO, () => ({ disponivel: true, carregados: false }), {
            'POST /api/v1/configuracoes/exemplos': () =>
                json(
                    {
                        type: 'https://acordu.app.br/erros/transicao-invalida',
                        title: 'Transição de status inválida',
                        status: 422,
                        detail: 'Os dados de exemplo só podem ser carregados numa empresa em teste que ainda não tem dados.',
                    },
                    422,
                ),
        })
        await abrir('/')
        const regiao = await cartao()
        await userEvent.setup().click(within(regiao).getByRole('button', { name: 'Carregar dados de exemplo' }))
        expect(await within(regiao).findByRole('alert')).toHaveTextContent(/ainda não tem dados/)
    })

    it('Apagar_ComConfirmacao_ChamaAApiEAvisa (F14-94)', async () => {
        let carregados = true
        const chamadas = app(DONO, () => ({ disponivel: !carregados, carregados }), {
            'DELETE /api/v1/configuracoes/exemplos': () => {
                carregados = false
                return new Response(null, { status: 204 })
            },
        })
        await abrir('/')
        const regiao = await cartao()
        expect(within(regiao).getByText(/o que você cadastrou fora deles fica/)).toBeInTheDocument()
        const usuario = userEvent.setup()
        await usuario.click(within(regiao).getByRole('button', { name: 'Apagar dados de exemplo' }))
        const janela = await screen.findByRole('dialog', { name: 'Apagar os dados de exemplo?' })
        expect(within(janela).getByText(/inclusive o que a equipe criou dentro deles/)).toBeInTheDocument()
        expect(chamadas.some((c) => c.metodo === 'DELETE')).toBe(false)
        await usuario.click(within(janela).getByRole('button', { name: 'Apagar dados de exemplo' }))

        expect(await screen.findByText('Os dados de exemplo foram apagados.')).toBeInTheDocument()
        expect(
            chamadas.filter((c) => c.metodo === 'DELETE' && c.caminho === '/api/v1/configuracoes/exemplos'),
        ).toHaveLength(1)
        expect(await screen.findByRole('button', { name: 'Carregar dados de exemplo' })).toBeInTheDocument()
    })

    it('DadosDeExemplo_SemConfiguracaoGerenciar_NemConsultaNemMostra (F14-95)', async () => {
        const chamadas = app(['solicitacao.ver'], () => ({ disponivel: true, carregados: false }), {
            'GET /api/v1/solicitacoes': vazio,
        })
        await abrir('/')
        expect(await screen.findByRole('region', { name: /Apontado hoje/ })).toBeInTheDocument()
        expect(chamadas.some((c) => c.caminho === '/api/v1/configuracoes/exemplos')).toBe(false)
        expect(screen.queryByRole('button', { name: 'Carregar dados de exemplo' })).toBeNull()
    })

    it('DadosDeExemplo_EmpresaSoLeitura_NaoOferece (ADR 0015)', async () => {
        const chamadas = app(
            DONO,
            () => ({ disponivel: false, carregados: true }),
            {},
            {
                situacao: 'VENCIDA',
                somenteLeituraDesde: '2026-11-10T09:00:00Z',
                expurgoPrevistoEm: '2026-12-10T09:00:00Z',
            },
        )
        await abrir('/')
        expect(await screen.findByRole('region', { name: /Apontado hoje/ })).toBeInTheDocument()
        expect(chamadas.some((c) => c.caminho === '/api/v1/configuracoes/exemplos')).toBe(false)
        expect(screen.queryByRole('button', { name: 'Apagar dados de exemplo' })).toBeNull()
    })
})
