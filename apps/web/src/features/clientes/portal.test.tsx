import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, json, perfil, type Rotas } from '@/testes/app'

const C = 'c1'
const marta = {
    id: 'k1',
    nome: 'Marta Souza',
    email: 'marta@bomgrao.com',
    telefone: null,
    cargo: null,
    ativo: true,
    temConta: false,
    acessoPortal: null as null | Record<string, unknown>,
}
const cliente = (acessoPortal: null | Record<string, unknown>) => ({
    id: C,
    nome: 'Padaria Bom Grão',
    documento: null,
    observacoes: null,
    ativo: true,
    versao: '1',
    razaoSocial: null,
    inscricaoEstadual: null,
    email: null,
    telefone: null,
    endereco: null,
    responsavel: null,
    etiquetas: [],
    contatos: [{ ...marta, acessoPortal }],
})
const app = (permissoes: string[], rotas: Rotas) =>
    comPerfil(permissoes, { 'GET /api/v1/equipe': () => json([]), ...rotas })
const PORTAL = ['cliente.ver', 'portal.gerenciar_acessos', 'solicitacao.ver', 'tarefa.ver']

describe('ficha: portal, arquivos e movimentos', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('convida com "Só as próprias" por padrão', async () => {
        let acesso: null | Record<string, unknown> = null
        const chamadas = app(PORTAL, {
            [`GET /api/v1/clientes/${C}`]: () => json(cliente(acesso)),
            [`POST /api/v1/clientes/${C}/contatos/k1/acesso-portal`]: () => {
                acesso = { situacao: 'CONVIDADO', escopoPortal: 'PROPRIAS', conviteExpiraEm: '2099-01-01T12:00:00Z' }
                return json({ ...marta, acessoPortal: acesso }, 202)
            },
        })
        await abrir(`/clientes/${C}?secao=contatos`)
        const usuario = userEvent.setup()
        expect(await screen.findByText('sem acesso ao portal')).toBeInTheDocument()
        // Sem cliente.gerenciar: só as ações do portal.
        expect(screen.queryByRole('button', { name: 'Editar Marta Souza' })).not.toBeInTheDocument()

        await usuario.click(screen.getByRole('button', { name: 'Convidar Marta Souza para o portal' }))
        const janela = await screen.findByRole('dialog', { name: 'Convidar para o portal' })
        expect(within(janela).getByRole('radio', { name: /Só as próprias/ })).toBeChecked()
        await usuario.click(within(janela).getByRole('button', { name: 'Enviar convite' }))
        expect(await screen.findByText('Convite enviado para marta@bomgrao.com.')).toBeInTheDocument()
        expect(chamadas.find((c) => c.metodo === 'POST' && c.caminho.endsWith('/acesso-portal'))?.corpo).toEqual({
            escopoPortal: 'PROPRIAS',
        })
        expect(await screen.findByText(/convidado · só as próprias · vale até/)).toBeInTheDocument()
        expect(screen.getByRole('button', { name: 'Reenviar convite de Marta Souza' })).toBeInTheDocument()
    })

    it('muda o escopo de quem já acessa e revoga com confirmação', async () => {
        const chamadas = app(PORTAL, {
            [`GET /api/v1/clientes/${C}`]: () =>
                json(cliente({ situacao: 'ATIVO', escopoPortal: 'PROPRIAS', conviteExpiraEm: null })),
            [`PATCH /api/v1/clientes/${C}/contatos/k1/acesso-portal`]: () => json(marta),
            [`DELETE /api/v1/clientes/${C}/contatos/k1/acesso-portal`]: () => new Response(null, { status: 204 }),
        })
        await abrir(`/clientes/${C}?secao=contatos`)
        const usuario = userEvent.setup()

        await usuario.click(await screen.findByRole('button', { name: 'Mudar acesso de Marta Souza' }))
        const janela = await screen.findByRole('dialog', { name: 'Mudar acesso ao portal' })
        await usuario.click(within(janela).getByRole('radio', { name: /Todas do cliente/ }))
        await usuario.click(within(janela).getByRole('button', { name: 'Salvar' }))
        expect(await screen.findByText('Acesso de Marta Souza alterado.')).toBeInTheDocument()
        expect(chamadas.find((c) => c.metodo === 'PATCH')?.corpo).toEqual({ escopoPortal: 'TODAS_DO_CLIENTE' })

        await usuario.click(screen.getByRole('button', { name: 'Revogar acesso de Marta Souza' }))
        const confirmacao = await screen.findByRole('dialog', { name: 'Revogar o acesso de Marta Souza?' })
        await usuario.click(within(confirmacao).getByRole('button', { name: 'Revogar acesso' }))
        expect(await screen.findByText('Marta Souza não acessa mais o portal.')).toBeInTheDocument()
        expect(chamadas.some((c) => c.metodo === 'DELETE')).toBe(true)
    })

    it('convite vencido aparece como expirado', async () => {
        app(PORTAL, {
            [`GET /api/v1/clientes/${C}`]: () =>
                json(
                    cliente({
                        situacao: 'CONVIDADO',
                        escopoPortal: 'TODAS_DO_CLIENTE',
                        conviteExpiraEm: '2020-01-01T00:00:00Z',
                    }),
                ),
        })
        await abrir(`/clientes/${C}?secao=contatos`)
        expect(await screen.findByText('convite expirado · todas do cliente')).toBeInTheDocument()
    })

    it('arquivos listam a origem, marcam o interno e baixam', async () => {
        const chamadas = app(PORTAL, {
            [`GET /api/v1/clientes/${C}`]: () => json(cliente(null)),
            [`GET /api/v1/clientes/${C}/arquivos`]: () =>
                json({
                    itens: [
                        {
                            id: 'a1',
                            nomeArquivo: 'nota.pdf',
                            tipoConteudo: 'application/pdf',
                            tamanhoBytes: 2048,
                            enviadoEm: '2026-10-06T13:00:00Z',
                            autor: { id: 'k1', nome: 'Marta Souza' },
                            autorEhCliente: true,
                            interno: false,
                            origem: { tipo: 'SOLICITACAO', id: 's1', titulo: 'Impressora parou', numero: 12 },
                        },
                        {
                            id: 'a2',
                            nomeArquivo: 'cabo.jpg',
                            tipoConteudo: 'image/jpeg',
                            tamanhoBytes: 10,
                            enviadoEm: '2026-10-05T13:00:00Z',
                            autor: { id: 'u1', nome: 'Ana Prado' },
                            autorEhCliente: false,
                            interno: true,
                            origem: { tipo: 'TAREFA', id: 't1', titulo: 'Trocar cabo', numero: null },
                        },
                    ],
                    pagina: 1,
                    tamanhoPagina: 50,
                    total: 2,
                }),
            '/api/v1/anexos/a1/download': () => json({ url: 'http://localhost:9090/acordu-anexos/x?assinatura=1' }),
        })
        await abrir(`/clientes/${C}?secao=arquivos`)
        expect(await screen.findByRole('link', { name: /Impressora parou/ })).toHaveAttribute(
            'href',
            '/solicitacoes/s1',
        )
        expect(screen.getByRole('link', { name: 'Trocar cabo' })).toHaveAttribute('href', '/tarefas/t1')
        expect(screen.getAllByText('Interno')).toHaveLength(1)
        expect(screen.getByText(/Marta Souza \(cliente\)/)).toBeInTheDocument()
        await userEvent.setup().click(screen.getByRole('button', { name: 'Baixar nota.pdf' }))
        await vi.waitFor(() => expect(chamadas.some((c) => c.caminho === '/api/v1/anexos/a1/download')).toBe(true))
    })

    it('movimentos montam a frase e carregam mais pelo cursor', async () => {
        const atividade = (id: string, mudancas: Record<string, unknown>) => ({
            id,
            ocorridaEm: '2026-10-06T13:00:00Z',
            autor: { id: 'u1', nome: 'Ana Prado' },
            autorEhCliente: false,
            alvo: { tipo: 'SOLICITACAO', id: 's1', titulo: 'Impressora parou', numero: 12 },
            cliente: { id: C, nome: 'Padaria Bom Grão' },
            detalhe: null,
            ...mudancas,
        })
        const chamadas = app(PORTAL, {
            [`GET /api/v1/clientes/${C}`]: () => json(cliente(null)),
            'GET /api/v1/atividades': (_, url) =>
                url.searchParams.get('antes') === '77'
                    ? json({ itens: [atividade('a3', { tipo: 'SOLICITACAO_ABERTA', autor: null })], proximo: null })
                    : json({
                          itens: [
                              atividade('a1', {
                                  tipo: 'TAREFA_STATUS',
                                  alvo: { tipo: 'TAREFA', id: 't1', titulo: 'Trocar cabo', numero: null },
                                  detalhe: { statusDe: 'A_FAZER', statusPara: 'FAZENDO', responsavel: null },
                              }),
                              atividade('a4', {
                                  tipo: 'TAREFA_PROJETO',
                                  alvo: { tipo: 'TAREFA', id: 't1', titulo: 'Trocar cabo', numero: null },
                                  detalhe: {
                                      statusDe: null,
                                      statusPara: null,
                                      responsavel: null,
                                      projetoDe: { id: 'p1', nome: 'Site novo' },
                                      projetoPara: { id: 'p2', nome: 'Campanha' },
                                  },
                              }),
                              atividade('a2', {
                                  tipo: 'RESPOSTA_DO_CLIENTE',
                                  autor: { id: 'k1', nome: 'Marta Souza' },
                                  autorEhCliente: true,
                              }),
                          ],
                          proximo: 77,
                      }),
        })
        await abrir(`/clientes/${C}?secao=movimentos`)
        expect(await screen.findByText(/moveu para fazendo/)).toBeInTheDocument()
        expect(screen.getByText(/\(cliente\) respondeu pelo portal/)).toBeInTheDocument()
        expect(screen.getByText(/passou do projeto Site novo para Campanha/)).toBeInTheDocument()
        expect(chamadas.find((c) => c.caminho === '/api/v1/atividades')?.consulta.get('clienteId')).toBe(C)

        await userEvent.setup().click(screen.getByRole('button', { name: 'Ver mais' }))
        expect(await screen.findByText('O sistema')).toBeInTheDocument()
        expect(screen.queryByRole('button', { name: 'Ver mais' })).not.toBeInTheDocument()
    })

    it('sem ver solicitações nem tarefas, não há arquivos nem movimentos', async () => {
        app(['cliente.ver'], { [`GET /api/v1/clientes/${C}`]: () => json(cliente(null)) })
        await abrir(`/clientes/${C}`)
        expect(await screen.findByRole('tab', { name: 'Contatos e portal' })).toBeInTheDocument()
        expect(screen.queryByRole('tab', { name: 'Arquivos' })).not.toBeInTheDocument()
        expect(screen.queryByRole('tab', { name: 'Movimentos' })).not.toBeInTheDocument()
    })

    it('empresa não liberada: convidar para o portal fica desabilitado, com a explicação (F9-43)', async () => {
        app(PORTAL, {
            '/api/v1/auth/eu': () =>
                json({ ...perfil, permissoes: PORTAL, tenant: { ...perfil.tenant, envioLiberado: false } }),
            [`GET /api/v1/clientes/${C}`]: () => json(cliente(null)),
        })
        await abrir(`/clientes/${C}?secao=contatos`)
        expect(await screen.findByRole('button', { name: 'Convidar Marta Souza para o portal' })).toBeDisabled()
        expect(screen.getByText('Disponível depois que você confirmar seu e-mail.')).toBeInTheDocument()
    })

    it('empresa não liberada: reenviar o convite também fica desabilitado, com a explicação (F9-43)', async () => {
        app(PORTAL, {
            '/api/v1/auth/eu': () =>
                json({ ...perfil, permissoes: PORTAL, tenant: { ...perfil.tenant, envioLiberado: false } }),
            [`GET /api/v1/clientes/${C}`]: () =>
                json(
                    cliente({
                        situacao: 'CONVIDADO',
                        escopoPortal: 'PROPRIAS',
                        conviteExpiraEm: '2099-01-01T12:00:00Z',
                    }),
                ),
        })
        await abrir(`/clientes/${C}?secao=contatos`)
        const reenviar = await screen.findByRole('button', { name: 'Reenviar convite de Marta Souza' })
        expect(reenviar).toBeDisabled()
        expect(reenviar).toHaveAccessibleDescription('Disponível depois que você confirmar seu e-mail.')
    })
})
