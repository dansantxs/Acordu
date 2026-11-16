import { screen, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { guardarTokenDeLink } from '@/shared/sessao/tokenDaUrl'
import { abrir, comContato, json, servidor } from '@/testes/app'
import { VERSOES_TEXTOS_LEGAIS } from './versoes'

/** Nome dos links do pé: o texto visível e o aviso de outra aba, só para o leitor de tela. */
const POLITICA_NOVA_ABA = /^Política de privacidade ?\(abre em outra aba\)$/
const TERMOS_NOVA_ABA = /^Termos de uso ?\(abre em outra aba\)$/

const semSessao = () => json({ status: 401 }, 401)

/** Os links dos textos legais da tela (a navegação "Textos legais"). */
const linksLegais = async () => within(await screen.findByRole('navigation', { name: 'Textos legais' }))

describe('textos legais: páginas públicas (F14-78)', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('PaginaPrivacidade_NinguemLogado_AbreComVersaoEVigencia', async () => {
        const chamadas = servidor({ '/api/v1/auth/refresh': semSessao })
        const roteador = await abrir('/privacidade')
        expect(await screen.findByRole('heading', { level: 1, name: 'Política de privacidade' })).toBeInTheDocument()
        const { versao, vigencia } = VERSOES_TEXTOS_LEGAIS.privacidade
        const [ano, mes, dia] = vigencia.split('-')
        expect(screen.getByText(`Versão ${versao}, em vigor desde ${dia}/${mes}/${ano}`)).toBeInTheDocument()
        // Fica na página (sem ir para o login) e só a renovação de sessão da carga do app toca a API.
        expect(roteador.state.location.pathname).toBe('/privacidade')
        expect(chamadas.map((c) => c.caminho)).toEqual(['/api/v1/auth/refresh'])
    })

    it('PaginaTermos_NinguemLogado_AbreComVersaoEVigencia', async () => {
        servidor({ '/api/v1/auth/refresh': semSessao })
        const roteador = await abrir('/termos')
        expect(await screen.findByRole('heading', { level: 1, name: 'Termos de uso' })).toBeInTheDocument()
        const { versao, vigencia } = VERSOES_TEXTOS_LEGAIS.termos
        const [ano, mes, dia] = vigencia.split('-')
        expect(screen.getByText(`Versão ${versao}, em vigor desde ${dia}/${mes}/${ano}`)).toBeInTheDocument()
        expect(roteador.state.location.pathname).toBe('/termos')
        expect(screen.getByRole('link', { name: 'Política de privacidade' })).toHaveAttribute('href', '/privacidade')
    })

    it('PaginaTermos_SaiDaPagina_RestauraOTituloDaAba', async () => {
        servidor({ '/api/v1/auth/refresh': semSessao })
        document.title = 'Antes'
        const roteador = await abrir('/termos')
        expect(await screen.findByRole('heading', { level: 1, name: 'Termos de uso' })).toBeInTheDocument()
        expect(document.title).toBe('Termos de uso · Acordu')
        await roteador.navigate({ to: '/entrar' })
        expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument()
        expect(document.title).toBe('Antes')
    })

    it('PaginaTermos_ApiForaDoAr_AbreMesmoAssim', async () => {
        // Sem nenhuma rota: a renovação da carga falha como sem rede, e a página não depende dela.
        servidor({})
        await abrir('/termos')
        expect(await screen.findByRole('heading', { level: 1, name: 'Termos de uso' })).toBeInTheDocument()
        expect(screen.getByRole('heading', { level: 2, name: /Disponibilidade/ })).toBeInTheDocument()
    })
})

describe('textos legais: links nas telas (F14-84)', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => {
        vi.unstubAllGlobals()
        sessionStorage.clear()
    })

    it('TelaEntrar_Abre_TemLinksParaPoliticaETermosSemPedirAceite', async () => {
        servidor({ '/api/v1/auth/refresh': semSessao })
        await abrir('/entrar')
        const links = await linksLegais()
        expect(links.getByRole('link', { name: POLITICA_NOVA_ABA })).toHaveAttribute('href', '/privacidade')
        expect(links.getByRole('link', { name: TERMOS_NOVA_ABA })).toHaveAttribute('href', '/termos')
        expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
    })

    it('AceiteConvitePortal_Abre_TemLinkParaPoliticaSemTermosNemAceite', async () => {
        servidor({
            '/api/v1/auth/refresh': semSessao,
            'POST /api/v1/auth/portal/convite/consulta': () =>
                json({
                    nomePrestador: 'Prado Consultoria',
                    nomeContato: 'Marta Souza',
                    email: 'marta@bomgrao.com',
                    temConta: false,
                }),
        })
        guardarTokenDeLink('convite-portal', 'tok123')
        await abrir('/portal/convite')
        expect(await screen.findByRole('textbox', { name: /Seu nome/ })).toBeInTheDocument()
        const links = await linksLegais()
        expect(links.getByRole('link', { name: POLITICA_NOVA_ABA })).toHaveAttribute('href', '/privacidade')
        expect(links.queryByRole('link', { name: /Termos de uso/ })).not.toBeInTheDocument()
        expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
    })

    it('Portal_ContatoComConta_TemLinkParaPoliticaSemTermosNemAceite', async () => {
        comContato({
            '/api/v1/portal/solicitacoes': () => json({ itens: [], pagina: 1, tamanhoPagina: 20, total: 0 }),
        })
        await abrir('/portal')
        const links = await linksLegais()
        expect(links.getByRole('link', { name: POLITICA_NOVA_ABA })).toHaveAttribute('href', '/privacidade')
        expect(links.queryByRole('link', { name: /Termos de uso/ })).not.toBeInTheDocument()
        expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
    })

    it('PortalPorLink_SemSessao_TemLinkParaPolitica', async () => {
        servidor({ '/api/v1/auth/refresh': semSessao })
        await abrir('/portal/acompanhar')
        const links = await linksLegais()
        expect(links.getByRole('link', { name: POLITICA_NOVA_ABA })).toHaveAttribute('href', '/privacidade')
        expect(links.queryByRole('link', { name: /Termos de uso/ })).not.toBeInTheDocument()
    })
})
