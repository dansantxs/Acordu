import { screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrirSessaoLink, reiniciarSessaoLinkParaTestes } from '@/shared/sessao/sessaoLink'
import { abrirSessaoOperacao, reiniciarSessaoOperacaoParaTestes, sessaoOperacao } from '@/shared/sessao/sessaoOperacao'
import { abrir, comContato, daquiA, semSessao, servidor } from '@/testes/app'

// F14-64 (F14m), complemento: o contato do portal e a sessão por link também não veem a área, em toda rota dela.

const ROTAS = [
    '/nebrys',
    '/nebrys/empresas',
    '/nebrys/empresas/0193a1b2-0000-7000-8000-000000000001',
    '/nebrys/documentos',
]

const tocouAArea = (chamadas: { caminho: string }[]) => chamadas.some((c) => c.caminho.startsWith('/api/v1/nebrys'))

describe('F14-64: contato do portal e sessão por link não veem a área da Nebrys', () => {
    beforeEach(() => {
        reiniciarParaTestes()
        reiniciarSessaoOperacaoParaTestes()
        reiniciarSessaoLinkParaTestes()
        localStorage.clear()
        sessionStorage.clear()
    })
    afterEach(() => vi.unstubAllGlobals())

    it.each(ROTAS)('Rota_SessaoDoContatoDoPortal_NaoEncontrado (%s)', async (caminho) => {
        const chamadas = comContato()
        await abrir(caminho)
        expect(await screen.findByText('Página não encontrada.')).toBeInTheDocument()
        expect(screen.queryByRole('heading', { name: 'Área da Nebrys' })).not.toBeInTheDocument()
        expect(screen.queryByRole('heading', { name: 'Empresas' })).not.toBeInTheDocument()
        expect(tocouAArea(chamadas)).toBe(false)
    })

    it.each(ROTAS)('Rota_SessaoPorLink_NaoEncontrado (%s)', async (caminho) => {
        abrirSessaoLink({ accessToken: 'link', expiraEm: daquiA(3_600_000), solicitacaoId: 's1' })
        const chamadas = servidor({ '/api/v1/auth/refresh': semSessao })
        await abrir(caminho)
        expect(await screen.findByText('Página não encontrada.')).toBeInTheDocument()
        expect(screen.queryByRole('heading', { name: 'Área da Nebrys' })).not.toBeInTheDocument()
        expect(screen.queryByRole('heading', { name: 'Empresas' })).not.toBeInTheDocument()
        expect(tocouAArea(chamadas)).toBe(false)
    })

    it('Rota_ContatoDoPortalComSessaoDeOperacaoAberta_EncerraAOperacaoENaoChamaAArea', async () => {
        abrirSessaoOperacao({ accessToken: 'op', expiraEm: daquiA(30 * 60_000) })
        const chamadas = comContato()
        await abrir('/nebrys/empresas')
        expect(await screen.findByText('Página não encontrada.')).toBeInTheDocument()
        expect(sessaoOperacao()).toBeNull()
        expect(tocouAArea(chamadas)).toBe(false)
    })

    it('Rota_SessaoPorLinkComSessaoDeOperacaoAberta_EncerraAOperacaoENaoChamaAArea', async () => {
        abrirSessaoOperacao({ accessToken: 'op', expiraEm: daquiA(30 * 60_000) })
        abrirSessaoLink({ accessToken: 'link', expiraEm: daquiA(3_600_000), solicitacaoId: 's1' })
        const chamadas = servidor({ '/api/v1/auth/refresh': semSessao })
        await abrir('/nebrys')
        expect(await screen.findByText('Página não encontrada.')).toBeInTheDocument()
        expect(sessaoOperacao()).toBeNull()
        expect(tocouAArea(chamadas)).toBe(false)
    })

    it('Rota_EnderecoInexistenteDaAreaSemOperacao_VaiParaAEntrada', async () => {
        const chamadas = servidor({ '/api/v1/auth/refresh': semSessao })
        const roteador = await abrir('/nebrys/qualquer-coisa')
        expect(await screen.findByRole('heading', { name: 'Área da Nebrys' })).toBeInTheDocument()
        expect(roteador.state.location.pathname).toBe('/nebrys')
        expect(tocouAArea(chamadas)).toBe(false)
    })
})
