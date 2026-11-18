import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { guardarTokenDeLink } from '@/shared/sessao/tokenDaUrl'
import { abrir, comContato, comSessao, json, perfil, semSessao, servidor, type Rotas } from '@/testes/app'

/** Aceite dos textos legais com os textos publicados (o `vite.config.ts` liga a configuração; spec do F14, F14h). */

const semAvisos = () => json({ itens: [], pagina: 1, tamanhoPagina: 10, total: 0, naoLidas: 0 })
const pagina = () => json({ itens: [], pagina: 1, tamanhoPagina: 50, total: 0 })

async function preencherCadastro() {
    const usuario = userEvent.setup()
    await usuario.type(await screen.findByLabelText('Seu nome'), 'Ana')
    await usuario.type(screen.getByLabelText('E-mail'), 'ana@exemplo.com')
    await usuario.type(screen.getByLabelText('Senha'), 'umasenhaboa')
    await usuario.type(screen.getByLabelText('Nome da empresa'), 'Prado')
    await usuario.type(screen.getByLabelText('CPF ou CNPJ'), '52998224725')
    return usuario
}

const caixa = () => screen.getByRole('checkbox', { name: /Li e aceito os termos de uso e a política de privacidade/ })

/** Os links ao lado da caixa (o pé da capa também tem os seus, no `nav` "Textos legais"). */
function linksDoAceite() {
    const bloco = screen.getByRole('group', { name: 'Aceite dos textos legais' })
    return {
        termos: within(bloco).getByRole('link', { name: /termos de uso/ }),
        privacidade: within(bloco).getByRole('link', { name: /política de privacidade/ }),
    }
}

function equipe(pendentes: () => unknown[], rotas: Rotas = {}) {
    return servidor({
        '/api/v1/auth/refresh': comSessao,
        '/api/v1/auth/eu': () => json({ ...perfil, permissoes: ['cliente.ver'], textosPendentes: pendentes() }),
        '/api/v1/notificacoes': semAvisos,
        'GET /api/v1/clientes': pagina,
        'GET /api/v1/equipe': () => json([{ id: 'u1', nome: 'Ana Prado' }]),
        'GET /api/v1/clientes/etiquetas': () => json([]),
        ...rotas,
    })
}

describe('aceite dos textos legais, publicados', () => {
    beforeEach(() => {
        reiniciarParaTestes()
        sessionStorage.clear()
    })
    afterEach(() => vi.unstubAllGlobals())

    it('Cadastro_Abre_TemLinksECaixaDesmarcadaObrigatoria (F14-79)', async () => {
        const chamadas = servidor({ '/api/v1/auth/refresh': semSessao })
        await abrir('/cadastro')
        const usuario = await preencherCadastro()

        expect(caixa()).not.toBeChecked()
        expect(caixa()).toBeRequired()
        const { termos, privacidade } = linksDoAceite()
        expect(termos).toHaveAttribute('href', '/termos')
        expect(termos).toHaveAttribute('target', '_blank')
        expect(privacidade).toHaveAttribute('href', '/privacidade')

        // Sem marcar, o formulário não envia.
        await usuario.click(screen.getByRole('button', { name: 'Criar conta' }))
        expect(chamadas.some((c) => c.caminho === '/api/v1/auth/registro')).toBe(false)
    })

    it('Cadastro_ComAceite_MandaAsVersoesVigentes (F14-81)', async () => {
        const chamadas = servidor({
            '/api/v1/auth/refresh': semSessao,
            '/api/v1/auth/registro': () => comSessao(),
            '/api/v1/auth/eu': () => json(perfil),
            '/api/v1/notificacoes': semAvisos,
        })
        await abrir('/cadastro')
        const usuario = await preencherCadastro()
        await usuario.click(caixa())
        await usuario.click(screen.getByRole('button', { name: 'Criar conta' }))

        await waitFor(() => expect(chamadas.some((c) => c.caminho === '/api/v1/auth/registro')).toBe(true))
        expect(chamadas.find((c) => c.caminho === '/api/v1/auth/registro')?.corpo).toMatchObject({
            versaoTermos: '1',
            versaoPrivacidade: '1',
        })
    })

    it('Cadastro_ApiRecusaOAceite_MostraARecusaJuntoDaCaixa (F14-80)', async () => {
        servidor({
            '/api/v1/auth/refresh': semSessao,
            '/api/v1/auth/registro': () =>
                json(
                    {
                        type: 'https://acordu.app.br/erros/validacao',
                        status: 400,
                        errors: { versaoTermos: ['Os termos de uso ou a política de privacidade mudaram.'] },
                    },
                    400,
                ),
        })
        await abrir('/cadastro')
        const usuario = await preencherCadastro()
        await usuario.click(caixa())
        await usuario.click(screen.getByRole('button', { name: 'Criar conta' }))

        const recusa = await screen.findByRole('alert')
        expect(recusa).toHaveTextContent('Os termos de uso ou a política de privacidade mudaram.')
        expect(within(screen.getByRole('group', { name: 'Aceite dos textos legais' })).getByRole('alert')).toBe(recusa)
    })

    it.each([
        ['ContaNova', false],
        ['ContaExistente', true],
    ])('AceiteConviteEquipe_%s_TemLinksECaixaObrigatoriaEMandaAsVersoes (F14-82)', async (_, temConta) => {
        const chamadas = servidor({
            '/api/v1/auth/refresh': semSessao,
            '/api/v1/auth/convite/consulta': () =>
                json({ nomeTenant: 'Prado Consultoria', email: 'bia@exemplo.com', papel: 'Membro', temConta }),
            '/api/v1/auth/convite': () => comSessao(),
            '/api/v1/auth/eu': () => json(perfil),
            '/api/v1/notificacoes': semAvisos,
        })
        await abrir('/convite/tok-convite')
        const usuario = userEvent.setup()
        if (temConta) {
            await usuario.type(await screen.findByLabelText('Senha da sua conta'), 'umasenhaboa')
        } else {
            await usuario.type(await screen.findByLabelText('Seu nome'), 'Bia')
            await usuario.type(screen.getByLabelText('Crie uma senha'), 'umasenhaboa')
        }
        expect(caixa()).not.toBeChecked()
        expect(caixa()).toBeRequired()
        expect(linksDoAceite().termos).toHaveAttribute('href', '/termos')
        expect(linksDoAceite().privacidade).toHaveAttribute('href', '/privacidade')

        await usuario.click(screen.getByRole('button', { name: 'Entrar na equipe' }))
        expect(chamadas.some((c) => c.caminho === '/api/v1/auth/convite')).toBe(false)

        await usuario.click(caixa())
        await usuario.click(screen.getByRole('button', { name: 'Entrar na equipe' }))
        await waitFor(() => expect(chamadas.some((c) => c.caminho === '/api/v1/auth/convite')).toBe(true))
        expect(chamadas.find((c) => c.caminho === '/api/v1/auth/convite')?.corpo).toMatchObject({
            versaoTermos: '1',
            versaoPrivacidade: '1',
        })
    })

    it('Casca_VersaoNovaPendente_AvisaComOLinkAteOCienteSemBloquear (F14-83)', async () => {
        let pendentes: unknown[] = [{ texto: 'TERMOS', versao: '2' }]
        const chamadas = equipe(() => pendentes, {
            'POST /api/v1/auth/textos/ciente': () => {
                pendentes = []
                return new Response(null, { status: 204 })
            },
        })
        await abrir('/clientes')

        const aviso = await screen.findByRole('status', { name: 'Textos legais' })
        expect(within(aviso).getByRole('link', { name: /termos de uso/ })).toHaveAttribute('href', '/termos')
        expect(within(aviso).queryByRole('link', { name: /política de privacidade/ })).not.toBeInTheDocument()
        // O uso não é bloqueado: a tela abre atrás do aviso.
        expect(await screen.findByRole('heading', { level: 1, name: 'Clientes' })).toBeInTheDocument()

        await userEvent.setup().click(within(aviso).getByRole('button', { name: 'Ciente' }))
        await waitFor(() => expect(screen.queryByRole('status', { name: 'Textos legais' })).not.toBeInTheDocument())
        expect(chamadas.filter((c) => c.metodo === 'POST' && c.caminho === '/api/v1/auth/textos/ciente')).toHaveLength(
            1,
        )
    })

    it('Casca_SemAceiteNenhum_AvisaDosDoisTextos (F14-119)', async () => {
        equipe(() => [
            { texto: 'TERMOS', versao: '1' },
            { texto: 'PRIVACIDADE', versao: '1' },
        ])
        await abrir('/clientes')

        const aviso = await screen.findByRole('status', { name: 'Textos legais' })
        expect(within(aviso).getByRole('link', { name: /termos de uso/ })).toHaveAttribute('href', '/termos')
        expect(within(aviso).getByRole('link', { name: /política de privacidade/ })).toHaveAttribute(
            'href',
            '/privacidade',
        )
        expect(within(aviso).getByRole('button', { name: 'Ciente' })).toBeEnabled()
    })

    it('Casca_SemPendentes_SemAviso (F14-83)', async () => {
        equipe(() => [])
        await abrir('/clientes')
        expect(await screen.findByRole('heading', { level: 1, name: 'Clientes' })).toBeInTheDocument()
        expect(screen.queryByRole('status', { name: 'Textos legais' })).not.toBeInTheDocument()
    })

    it('Casca_EmpresaSomenteLeitura_CienteSegueDisponivel (F14-83)', async () => {
        servidor({
            '/api/v1/auth/refresh': comSessao,
            '/api/v1/auth/eu': () =>
                json({
                    ...perfil,
                    permissoes: ['cliente.ver'],
                    tenant: { ...perfil.tenant, situacao: 'VENCIDA', somenteLeituraDesde: '2026-11-10T09:00:00Z' },
                    textosPendentes: [{ texto: 'PRIVACIDADE', versao: '1' }],
                }),
            '/api/v1/notificacoes': semAvisos,
            'GET /api/v1/clientes': pagina,
            'GET /api/v1/equipe': () => json([]),
            'GET /api/v1/clientes/etiquetas': () => json([]),
        })
        await abrir('/clientes')
        const aviso = await screen.findByRole('status', { name: 'Textos legais' })
        expect(within(aviso).getByRole('button', { name: 'Ciente' })).toBeEnabled()
    })

    it('Portal_ContatoComPendentesNaSessao_NuncaMostraAvisoNemCaixa (F14-120)', async () => {
        comContato({
            '/api/v1/auth/eu': () =>
                json({
                    ...perfil,
                    nome: 'Marta Souza',
                    papel: 'Cliente',
                    clienteId: 'c1',
                    escopoPortal: 'TODAS_DO_CLIENTE',
                    textosPendentes: [{ texto: 'TERMOS', versao: '1' }],
                }),
            '/api/v1/portal/solicitacoes': () => json({ itens: [], pagina: 1, tamanhoPagina: 20, total: 0 }),
        })
        await abrir('/portal')
        expect(await screen.findByText(/atendimento de .*, pelo Acordu/)).toBeInTheDocument()
        expect(screen.queryByRole('status', { name: 'Textos legais' })).not.toBeInTheDocument()
        expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
    })

    it('AceiteConvitePortal_Publicados_SemCaixaDeAceite (F14-120)', async () => {
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
        expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
    })
})
