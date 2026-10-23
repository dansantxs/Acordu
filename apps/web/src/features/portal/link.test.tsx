import { screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { reiniciarSessaoLinkParaTestes } from '@/shared/sessao/sessaoLink'
import { abrir, json, servidor, type Rotas } from '@/testes/app'

const detalhe = {
    id: 's1',
    numero: 1042,
    titulo: 'Ajustar banner da campanha',
    descricao: null,
    statusExterno: 'AGUARDANDO_VOCE',
    abertaEm: '2026-10-20T12:00:00Z',
    responsavel: { nome: 'Daniel' },
    sla: null,
    tarefasVisiveis: [],
    tempoVisivelMinutos: 0,
    podeAprovar: true,
}
const eu = {
    contato: { id: 'k1', nome: 'Carlos Prado', email: 'carlos@bomgrao.com' },
    cliente: { id: 'c1', nome: 'Padaria Bom Grão' },
    prestador: { nome: 'Prado Consultoria', logoUrl: null },
    escopoPortal: 'PROPRIAS',
    veContratos: false,
    solicitacaoId: 's1',
}
const semConta = (rotas: Rotas) => servidor({ '/api/v1/auth/refresh': () => json({ status: 401 }, 401), ...rotas })

describe('portal: acesso por link', () => {
    beforeEach(() => {
        reiniciarParaTestes()
        reiniciarSessaoLinkParaTestes()
    })
    afterEach(() => vi.unstubAllGlobals())

    it('troca o token, tira da URL e mostra só a solicitação do link, sem navegação', async () => {
        const chamadas = semConta({
            'POST /api/v1/auth/portal/link': () =>
                json({
                    accessToken: 'l',
                    expiraEm: '2099-01-01T00:00:00Z',
                    solicitacaoId: 's1',
                    tipo: 'LINK_SOLICITACAO',
                }),
            '/api/v1/portal/eu': () => json(eu),
            '/api/v1/portal/solicitacoes/s1': () => json(detalhe),
            '/api/v1/portal/solicitacoes/s1/interacoes': () => json([]),
        })
        const roteador = await abrir('/portal/l/tok-do-email')
        expect(await screen.findByRole('heading', { name: 'Ajustar banner da campanha' })).toBeInTheDocument()
        expect(roteador.state.location.pathname).toBe('/portal/acompanhar')
        expect(screen.getByRole('region', { name: /A entrega está pronta/ })).toBeInTheDocument()
        expect(screen.queryByRole('navigation', { name: 'Portal' })).not.toBeInTheDocument()
        expect(screen.queryByRole('link', { name: '← Solicitações' })).not.toBeInTheDocument()
        expect(chamadas.find((c) => c.caminho === '/api/v1/auth/portal/link')?.corpo).toEqual({ token: 'tok-do-email' })
    })

    it('link inválido explica e oferece entrar com conta', async () => {
        semConta({ 'POST /api/v1/auth/portal/link': () => json({ status: 401 }, 401) })
        await abrir('/portal/l/velho')
        expect(await screen.findByRole('heading', { name: 'Este link não vale mais' })).toBeInTheDocument()
        expect(screen.getByRole('link', { name: 'Entre com e-mail e senha' })).toBeInTheDocument()
    })

    it('recarregar (sem a sessão na memória) pede o link de novo', async () => {
        semConta({})
        await abrir('/portal/acompanhar')
        expect(await screen.findByRole('heading', { name: 'Abra o link do e-mail de novo' })).toBeInTheDocument()
    })

    it('401 no meio do uso encerra o acesso por link', async () => {
        semConta({
            'POST /api/v1/auth/portal/link': () =>
                json({
                    accessToken: 'l',
                    expiraEm: '2099-01-01T00:00:00Z',
                    solicitacaoId: 's1',
                    tipo: 'LINK_SOLICITACAO',
                }),
            '/api/v1/portal/eu': () => json(eu),
            '/api/v1/portal/solicitacoes/s1': () => json(detalhe),
            '/api/v1/portal/solicitacoes/s1/interacoes': () => json({ status: 401 }, 401),
        })
        await abrir('/portal/l/tok')
        expect(await screen.findByRole('heading', { name: 'O acesso por este link acabou' })).toBeInTheDocument()
    })
})
