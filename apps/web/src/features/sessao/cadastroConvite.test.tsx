import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes, tokenAtual } from '@/shared/sessao/sessao'
import { descartarTokenDeLink, lerTokenDeLink } from '@/shared/sessao/tokenDaUrl'
import { abrir, comSessao, daquiA, json, perfil, semSessao, servidor, type Rotas } from '@/testes/app'

// Spec do F14, cadastro controlado nas telas (F14q): F14-44, F14-46 e F14-109. Build sem a chave do Turnstile.

const modo = (valor: 'CONVITE' | 'ABERTO') => () => json({ modo: valor })
const semAvisos = () => json({ itens: [], pagina: 1, tamanhoPagina: 10, total: 0, naoLidas: 0 })

const recusa = (status: number, errors: Record<string, string[]> = {}) =>
    json({ type: 'https://acordu.app.br/erros/validacao', status, title: 'Recusado', errors }, status)

function semConta(rotas: Rotas) {
    return servidor({ '/api/v1/auth/refresh': semSessao, ...rotas })
}

async function preencherLista() {
    const usuario = userEvent.setup()
    await usuario.type(await screen.findByLabelText('Seu nome'), 'Ana Souza')
    await usuario.type(screen.getByLabelText('E-mail'), 'ana@estudiotraco.com.br')
    await usuario.type(screen.getByLabelText('Nome da empresa'), 'Estúdio Traço')
    return usuario
}

async function preencherCadastroPorConvite() {
    const usuario = userEvent.setup()
    await usuario.type(await screen.findByLabelText('Seu nome'), 'Ana Souza')
    await usuario.type(screen.getByLabelText('Senha'), 'umasenhaboa')
    await usuario.type(screen.getByLabelText('Nome da empresa'), 'Estúdio Traço')
    await usuario.type(screen.getByLabelText('CPF ou CNPJ'), '52998224725')
    await usuario.click(screen.getByRole('checkbox', { name: /Li e aceito/ }))
    return usuario
}

describe('cadastro no modo convite', () => {
    beforeEach(() => {
        reiniciarParaTestes()
        sessionStorage.clear()
        // O token também fica na memória do módulo (sem storage): cada teste começa sem nenhum.
        descartarTokenDeLink('convite-empresa')
    })
    afterEach(() => vi.unstubAllGlobals())

    describe('F14-44: sem convite, a lista de espera no lugar do cadastro', () => {
        it('TelaCadastro_ModoConvite_MostraAListaDeEsperaComOMotivo', async () => {
            const chamadas = semConta({ 'GET /api/v1/auth/cadastro': modo('CONVITE') })
            await abrir('/cadastro')

            expect(await screen.findByRole('heading', { level: 1, name: 'Lista de espera' })).toBeInTheDocument()
            expect(screen.getByText(/No momento, o cadastro no Acordu é só por convite/)).toBeInTheDocument()
            expect(screen.getByLabelText('Seu nome')).toBeRequired()
            expect(screen.getByLabelText('E-mail')).toBeRequired()
            expect(screen.getByLabelText('Nome da empresa')).toBeRequired()
            expect(screen.getByLabelText('O que você quer organizar com o Acordu?')).not.toBeRequired()
            expect(screen.getByRole('button', { name: 'Entrar na lista de espera' })).toBeInTheDocument()
            // Nada do cadastro: sem senha, documento nem "Criar conta".
            expect(screen.queryByLabelText('Senha')).not.toBeInTheDocument()
            expect(screen.queryByLabelText('CPF ou CNPJ')).not.toBeInTheDocument()
            expect(screen.queryByRole('button', { name: 'Criar conta' })).not.toBeInTheDocument()
            expect(chamadas.some((c) => c.caminho === '/api/v1/auth/registro')).toBe(false)
        })

        it('TelaCadastro_ModoAberto_MostraOCadastroSemALista', async () => {
            semConta({ 'GET /api/v1/auth/cadastro': modo('ABERTO') })
            await abrir('/cadastro')

            expect(await screen.findByRole('heading', { level: 1, name: 'Criar conta' })).toBeInTheDocument()
            expect(await screen.findByLabelText('Senha')).toBeInTheDocument()
            expect(screen.getByRole('button', { name: 'Criar conta' })).toBeInTheDocument()
            expect(screen.queryByRole('button', { name: 'Entrar na lista de espera' })).not.toBeInTheDocument()
        })

        it('TelaCadastro_ModoNaoVeioEApiPedeConvite_TrocaParaAListaDeEspera', async () => {
            semConta({
                'GET /api/v1/auth/cadastro': () => json({ status: 503 }, 503),
                'POST /api/v1/auth/registro': () =>
                    recusa(400, { convite: ['No momento, o cadastro no Acordu é só por convite.'] }),
            })
            const usuario = userEvent.setup()
            await abrir('/cadastro')
            await usuario.type(await screen.findByLabelText('Seu nome'), 'Ana')
            await usuario.type(screen.getByLabelText('E-mail'), 'ana@exemplo.com')
            await usuario.type(screen.getByLabelText('Senha'), 'umasenhaboa')
            await usuario.type(screen.getByLabelText('Nome da empresa'), 'Prado')
            await usuario.type(screen.getByLabelText('CPF ou CNPJ'), '52998224725')
            await usuario.click(screen.getByRole('checkbox', { name: /Li e aceito/ }))
            await usuario.click(screen.getByRole('button', { name: 'Criar conta' }))

            expect(await screen.findByRole('heading', { level: 1, name: 'Lista de espera' })).toBeInTheDocument()
            expect(screen.getByRole('button', { name: 'Entrar na lista de espera' })).toBeInTheDocument()
        })

        it('ListaEspera_Envia_MandaOsCamposEConfirmaSemDizerSeJaEstava', async () => {
            const chamadas = semConta({
                'GET /api/v1/auth/cadastro': modo('CONVITE'),
                'POST /api/v1/lista-espera': () => new Response(null, { status: 202 }),
            })
            await abrir('/cadastro')
            const usuario = await preencherLista()
            await usuario.type(screen.getByLabelText('O que você quer organizar com o Acordu?'), 'Atendimento.')
            await usuario.click(screen.getByRole('button', { name: 'Entrar na lista de espera' }))

            expect(await screen.findByText(/Recebemos seus dados/)).toBeInTheDocument()
            expect(chamadas.find((c) => c.caminho === '/api/v1/lista-espera')?.corpo).toEqual({
                nome: 'Ana Souza',
                email: 'ana@estudiotraco.com.br',
                nomeEmpresa: 'Estúdio Traço',
                mensagem: 'Atendimento.',
            })
            expect(screen.queryByRole('button', { name: 'Entrar na lista de espera' })).not.toBeInTheDocument()
        })

        it('ListaEspera_SemMensagem_MandaNula', async () => {
            const chamadas = semConta({
                'GET /api/v1/auth/cadastro': modo('CONVITE'),
                'POST /api/v1/lista-espera': () => new Response(null, { status: 202 }),
            })
            await abrir('/cadastro')
            const usuario = await preencherLista()
            await usuario.click(screen.getByRole('button', { name: 'Entrar na lista de espera' }))

            await screen.findByText(/Recebemos seus dados/)
            expect(chamadas.find((c) => c.caminho === '/api/v1/lista-espera')?.corpo).toMatchObject({ mensagem: null })
        })

        it('ListaEspera_Recusa400_MostraNoCampo', async () => {
            const texto =
                'Use um e-mail que você vai continuar usando: endereços de e-mail temporário (descartável) não são aceitos.'
            semConta({
                'GET /api/v1/auth/cadastro': modo('CONVITE'),
                'POST /api/v1/lista-espera': () => recusa(400, { email: [texto] }),
            })
            await abrir('/cadastro')
            const usuario = await preencherLista()
            await usuario.click(screen.getByRole('button', { name: 'Entrar na lista de espera' }))

            expect(await screen.findByText(texto)).toBeInTheDocument()
            expect(screen.getByLabelText('E-mail')).toHaveAttribute('aria-invalid', 'true')
            expect(screen.queryByText(/Recebemos seus dados/)).not.toBeInTheDocument()
        })

        it('ListaEspera_Limite429_ExplicaQueEPorHora', async () => {
            semConta({
                'GET /api/v1/auth/cadastro': modo('CONVITE'),
                'POST /api/v1/lista-espera': () => json({ status: 429, title: 'Muitas requisições' }, 429),
            })
            await abrir('/cadastro')
            const usuario = await preencherLista()
            await usuario.click(screen.getByRole('button', { name: 'Entrar na lista de espera' }))

            expect(await screen.findByRole('alert')).toHaveTextContent(/Muitos envios a partir desta rede/)
        })
    })

    describe('F14-109: a lista de espera tem o link da política, sem aceite', () => {
        it('ListaEspera_Abre_TemLinkParaAPoliticaSemTermosNemAceite', async () => {
            semConta({ 'GET /api/v1/auth/cadastro': modo('CONVITE') })
            await abrir('/cadastro')
            await screen.findByRole('heading', { level: 1, name: 'Lista de espera' })

            const politica = screen.getByRole('link', { name: /Política de privacidade/ })
            expect(politica).toHaveAttribute('href', '/privacidade')
            expect(politica).toHaveAttribute('target', '_blank')
            expect(screen.queryByRole('link', { name: /Termos de uso/i })).not.toBeInTheDocument()
            expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
            expect(screen.queryByRole('group', { name: 'Aceite dos textos legais' })).not.toBeInTheDocument()
        })
    })

    describe('F14-46: o link do convite abre o cadastro com o e-mail fixo', () => {
        const convite =
            (temConta = false) =>
            () =>
                json({ email: 'ana@estudiotraco.com.br', temConta })

        it('LinkDoConvite_Abre_TiraOTokenDaUrlEMostraOEmailDoConviteSemDeixarTrocar', async () => {
            const chamadas = semConta({ 'POST /api/v1/auth/registro/convite/consulta': convite() })
            const roteador = await abrir('/cadastro/convite/tok-convite-empresa')

            const email = await screen.findByLabelText('E-mail')
            expect(roteador.state.location.pathname).toBe('/cadastro/convite')
            expect(roteador.state.location.href).not.toContain('tok-convite-empresa')
            expect(email).toHaveValue('ana@estudiotraco.com.br')
            expect(email).toHaveAttribute('readonly')
            expect(screen.getByText(/A Nebrys convidou/)).toHaveTextContent('ana@estudiotraco.com.br')
            expect(chamadas.find((c) => c.caminho === '/api/v1/auth/registro/convite/consulta')?.corpo).toEqual({
                token: 'tok-convite-empresa',
            })
            // A tela do convite não consulta o modo: o convite vale nos dois (contrato §2).
            expect(chamadas.some((c) => c.caminho === '/api/v1/auth/cadastro')).toBe(false)

            await userEvent.setup().type(email, 'outra@exemplo.com')
            expect(email).toHaveValue('ana@estudiotraco.com.br')
        })

        it('CadastroPorConvite_Envia_MandaOConviteEOEmailDoConviteEApagaOToken', async () => {
            const chamadas = semConta({
                'POST /api/v1/auth/registro/convite/consulta': convite(),
                'POST /api/v1/auth/registro': () => comSessao(),
                '/api/v1/auth/eu': () => json(perfil),
                '/api/v1/notificacoes': semAvisos,
            })
            const roteador = await abrir('/cadastro/convite/tok-convite-empresa')
            const usuario = await preencherCadastroPorConvite()
            await usuario.click(screen.getByRole('button', { name: 'Criar conta' }))

            await waitFor(() => expect(roteador.state.location.pathname).toBe('/'))
            expect(chamadas.find((c) => c.caminho === '/api/v1/auth/registro')?.corpo).toMatchObject({
                nome: 'Ana Souza',
                email: 'ana@estudiotraco.com.br',
                nomeEmpresa: 'Estúdio Traço',
                convite: 'tok-convite-empresa',
            })
            expect(lerTokenDeLink('convite-empresa')).toBeNull()
        })

        it('CadastroPorConvite_ComSessaoAtiva_MostraOFormularioEOCadastroTrocaASessao', async () => {
            // Quem já entrou numa empresa e abre o link: a rota não redireciona, e o cadastro abre a sessão da conta nova.
            servidor({
                '/api/v1/auth/refresh': comSessao,
                '/api/v1/auth/eu': () => json(perfil),
                '/api/v1/notificacoes': semAvisos,
                'POST /api/v1/auth/registro/convite/consulta': convite(),
                'POST /api/v1/auth/registro': () => json({ accessToken: 'b', expiraEm: daquiA(3_600_000) }),
            })
            const roteador = await abrir('/cadastro/convite/tok-convite-empresa')
            expect(tokenAtual()).toBe('a')

            expect(await screen.findByLabelText('E-mail')).toHaveValue('ana@estudiotraco.com.br')
            expect(roteador.state.location.pathname).toBe('/cadastro/convite')
            const usuario = await preencherCadastroPorConvite()
            await usuario.click(screen.getByRole('button', { name: 'Criar conta' }))

            await waitFor(() => expect(roteador.state.location.pathname).toBe('/'))
            expect(tokenAtual()).toBe('b')
            expect(lerTokenDeLink('convite-empresa')).toBeNull()
        })

        it('CadastroPorConvite_ApiRecusaOConvite_MostraAMensagemNoTopo', async () => {
            const texto = 'Este convite não vale mais: venceu, já foi usado ou foi revogado.'
            semConta({
                'POST /api/v1/auth/registro/convite/consulta': convite(),
                'POST /api/v1/auth/registro': () => recusa(400, { convite: [texto] }),
            })
            await abrir('/cadastro/convite/tok-convite-empresa')
            const usuario = await preencherCadastroPorConvite()
            await usuario.click(screen.getByRole('button', { name: 'Criar conta' }))

            expect(await screen.findByRole('alert')).toHaveTextContent(texto)
            // Com convite, a recusa não troca para a lista de espera.
            expect(screen.getByRole('button', { name: 'Criar conta' })).toBeInTheDocument()
        })

        it('CadastroPorConvite_TokenQueNaoVale_ExplicaSemFormulario', async () => {
            semConta({ 'POST /api/v1/auth/registro/convite/consulta': () => new Response(null, { status: 401 }) })
            await abrir('/cadastro/convite/tok-vencido')

            expect(await screen.findByRole('alert')).toHaveTextContent(/Este convite não vale mais/)
            expect(screen.getByRole('alert')).toHaveTextContent('contato@acordu.app.br')
            expect(screen.queryByLabelText('Senha')).not.toBeInTheDocument()
            expect(screen.getByRole('link', { name: 'Ir para o cadastro' })).toHaveAttribute('href', '/cadastro')
        })

        it('CadastroPorConvite_SemTokenNaAba_ExplicaSemChamarAApi', async () => {
            const chamadas = semConta({})
            await abrir('/cadastro/convite')

            expect(await screen.findByRole('alert')).toHaveTextContent(/Este convite não vale mais/)
            expect(chamadas.some((c) => c.caminho.startsWith('/api/v1/auth/registro'))).toBe(false)
        })

        it('CadastroPorConvite_EmailQueJaTemConta_ExplicaSemFormulario', async () => {
            semConta({ 'POST /api/v1/auth/registro/convite/consulta': convite(true) })
            await abrir('/cadastro/convite/tok-convite-empresa')

            const aviso = await screen.findByRole('status')
            expect(aviso).toHaveTextContent(/já tem conta no Acordu/)
            expect(within(aviso).getByText('ana@estudiotraco.com.br')).toBeInTheDocument()
            expect(screen.queryByLabelText('Senha')).not.toBeInTheDocument()
            expect(screen.getByRole('link', { name: 'Entrar com a conta que já existe' })).toHaveAttribute(
                'href',
                '/entrar',
            )
        })
    })
})
