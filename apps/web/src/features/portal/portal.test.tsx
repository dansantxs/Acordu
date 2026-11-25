import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comContato, comPerfil, json, servidor } from '@/testes/app'

const resumo = (id: string, numero: number, titulo: string, statusExterno: string, podeAprovar = false) => ({
    id,
    numero,
    titulo,
    statusExterno,
    abertaEm: '2026-10-20T12:00:00Z',
    podeAprovar,
})
const pagina = (itens: unknown[]) => json({ itens, pagina: 1, tamanhoPagina: 20, total: itens.length })

describe('portal: base', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('o portal não pede permissão nem mostra notificação do navegador (F12-25)', async () => {
        const pedir = vi.fn(async () => 'granted' as NotificationPermission)
        const criar = vi.fn()
        vi.stubGlobal(
            'Notification',
            class {
                static permission = 'granted'
                static requestPermission = pedir
                constructor() {
                    criar()
                }
            },
        )
        comContato({
            'GET /api/v1/portal/solicitacoes': () => pagina([resumo('s1', 12, 'Site fora do ar', 'EM_ANDAMENTO')]),
        })
        await abrir('/portal')
        expect(await screen.findByRole('link', { name: /Site fora do ar/ })).toBeInTheDocument()
        expect(pedir).not.toHaveBeenCalled()
        expect(criar).not.toHaveBeenCalled()
    })

    it('o portal não oferece "Sugerir melhoria" (F13-25)', async () => {
        comContato({
            'GET /api/v1/portal/solicitacoes': () => pagina([resumo('s1', 12, 'Site fora do ar', 'EM_ANDAMENTO')]),
        })
        await abrir('/portal')
        await screen.findByRole('link', { name: /Site fora do ar/ })
        await userEvent.click(screen.getByRole('button', { name: /, menu$/ }))
        const itens = (await screen.findAllByRole('menuitem')).map((i) => i.textContent)
        expect(itens).toEqual(['Sair'])
    })

    it('Portal_Lista_SemFalarNoWhatsApp (#208)', async () => {
        comContato({
            'GET /api/v1/portal/solicitacoes': () => pagina([resumo('s1', 12, 'Site fora do ar', 'EM_ANDAMENTO')]),
        })
        await abrir('/portal')
        await screen.findByRole('link', { name: /Site fora do ar/ })
        expect(screen.queryByRole('link', { name: /WhatsApp/ })).not.toBeInTheDocument()
    })

    it('Portal_Telas_NaoUsamOAtalhoDoWhatsApp (#208)', () => {
        const fontes = import.meta.glob(['./*.tsx', '!./*.test.tsx'], { query: '?raw', import: 'default', eager: true })
        expect(Object.keys(fontes).length).toBeGreaterThan(0)
        for (const [arquivo, fonte] of Object.entries(fontes))
            expect(String(fonte), arquivo).not.toMatch(/LinkWhatsApp|linkWhatsApp|wa\.me/)
    })

    it('lista em andamento, conta o que espera o contato e troca de grupo pelo endereço', async () => {
        const chamadas = comContato({
            'GET /api/v1/portal/solicitacoes': (_, url) =>
                url.searchParams.get('statusExterno') === 'AGUARDANDO_VOCE'
                    ? pagina([resumo('s2', 13, 'Logo nova', 'AGUARDANDO_VOCE', true)])
                    : pagina([resumo('s1', 12, 'Site fora do ar', 'EM_ANDAMENTO')]),
        })
        const roteador = await abrir('/portal')
        expect(await screen.findByText('Prado Consultoria')).toBeInTheDocument()
        expect(await screen.findByRole('link', { name: /Site fora do ar/ })).toHaveAttribute(
            'href',
            '/portal/solicitacoes/s1',
        )
        const aba = screen.getByRole('tab', { name: /Esperando você/ })
        expect(await within(aba).findByText('1')).toBeInTheDocument()
        expect(
            chamadas
                .find(
                    (c) =>
                        c.caminho === '/api/v1/portal/solicitacoes' &&
                        c.consulta.get('statusExterno') !== 'AGUARDANDO_VOCE',
                )
                ?.consulta.get('statusExterno'),
        ).toBe('RECEBIDA,EM_ANDAMENTO')

        await userEvent.setup().click(aba)
        await vi.waitFor(() => expect(roteador.state.location.search).toEqual({ grupo: 'esperando' }))
        expect(await screen.findByText(/entrega para você aprovar/)).toBeInTheDocument()
    })

    it('quem é da equipe não entra no portal', async () => {
        comPerfil([])
        const roteador = await abrir('/portal')
        await vi.waitFor(() => expect(roteador.state.location.pathname).toBe('/'))
    })

    it('aceita o convite com conta nova e cai no portal', async () => {
        const chamadas = servidor({
            '/api/v1/auth/refresh': () => json({ status: 401 }, 401),
            'POST /api/v1/auth/portal/convite/consulta': () =>
                json({
                    nomePrestador: 'Prado Consultoria',
                    nomeContato: 'Marta Souza',
                    email: 'marta@bomgrao.com',
                    temConta: false,
                }),
            'POST /api/v1/auth/portal/convite': () =>
                json({ accessToken: 'a', expiraEm: new Date(Date.now() + 3_600_000).toISOString() }, 201),
            '/api/v1/auth/eu': () =>
                json({
                    usuarioId: 'k1',
                    nome: 'Marta Souza',
                    email: 'marta@bomgrao.com',
                    emailConfirmado: true,
                    tenant: {
                        id: 't1',
                        nome: 'Prado Consultoria',
                        fusoHorario: 'America/Sao_Paulo',
                        envioLiberado: true,
                        situacao: 'TESTE',
                        fimDoTeste: '2026-12-13T12:00:00Z',
                        somenteLeituraDesde: null,
                        expurgoPrevistoEm: null,
                        diasFaixaTeste: 7,
                        demonstracao: false,
                    },
                    papel: 'Cliente',
                    permissoes: [],
                    clienteId: 'c1',
                    escopoPortal: 'PROPRIAS',
                    tenants: [],
                }),
            '/api/v1/portal/eu': () =>
                json({
                    contato: { id: 'k1', nome: 'Marta Souza', email: 'marta@bomgrao.com' },
                    cliente: { id: 'c1', nome: 'Padaria Bom Grão' },
                    prestador: { nome: 'Prado Consultoria', logoUrl: null },
                    escopoPortal: 'PROPRIAS',
                    veContratos: false,
                    envioDisponivel: true,
                    solicitacaoId: null,
                }),
            '/api/v1/portal/solicitacoes': () => pagina([]),
        })
        const roteador = await abrir('/portal/convite/tok123')
        await vi.waitFor(() => expect(roteador.state.location.pathname).toBe('/portal/convite'))
        const usuario = userEvent.setup()
        expect(await screen.findByRole('textbox', { name: /Seu nome/ })).toHaveValue('Marta Souza')
        await usuario.type(screen.getByLabelText(/Crie uma senha/), 'senha-bem-longa')
        await usuario.click(screen.getByRole('button', { name: 'Entrar no portal' }))
        await vi.waitFor(() => expect(roteador.state.location.pathname).toBe('/portal'))
        expect(chamadas.find((c) => c.caminho === '/api/v1/auth/portal/convite')?.corpo).toEqual({
            token: 'tok123',
            nome: 'Marta Souza',
            senha: 'senha-bem-longa',
        })
        expect(await screen.findByText('As suas, em Padaria Bom Grão')).toBeInTheDocument()
    })
})
