import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, daquiA, json, perfil, type Rotas } from '@/testes/app'

const ID = '0199f0a0-0000-7000-8000-0000000000a1'
const RAFAEL = '0199f0a0-0000-7000-8000-000000000002'

const solicitacao = {
    id: ID,
    numero: 1042,
    versao: '7',
    titulo: 'Forno não liga',
    descricao: null,
    cliente: { id: 'c1', nome: 'Padaria Bom Grão' },
    contato: { id: 'k1', nome: 'Marta Souza' },
    contratoId: null,
    categoriaId: null,
    prioridade: 'MEDIA',
    status: 'EM_ANDAMENTO',
    statusExterno: 'EM_ANDAMENTO',
    origem: 'PORTAL',
    responsavel: null,
    abertaEm: daquiA(-3_600_000),
    sla: null,
    situacaoSla: 'SEM_SLA',
    tempoApontadoMinutos: 0,
    tarefas: [],
    solucao: null,
    resolvidaEm: null,
    fechadaEm: null,
    canceladaEm: null,
    motivoCancelamento: null,
}

const interacao = (mudancas: Record<string, unknown>) => ({
    id: crypto.randomUUID(),
    autor: { id: perfil.usuarioId, nome: 'Ana Prado' },
    autorEhCliente: false,
    texto: null,
    visibilidade: 'INTERNA',
    registradaEm: daquiA(-60_000),
    anexos: [],
    ...mudancas,
})

const app = (permissoes: string[], rotas: Rotas = {}) =>
    comPerfil(permissoes, {
        'GET /api/v1/solicitacoes': () => json({ itens: [], pagina: 1, tamanhoPagina: 50, total: 0 }),
        [`GET /api/v1/solicitacoes/${ID}`]: () => json(solicitacao),
        'GET /api/v1/equipe': () =>
            json([
                { id: perfil.usuarioId, nome: 'Ana Prado' },
                { id: RAFAEL, nome: 'Rafael Moura' },
            ]),
        'GET /api/v1/categorias': () => json([]),
        [`GET /api/v1/solicitacoes/${ID}/seguidores`]: () => json([]),
        [`GET /api/v1/solicitacoes/${ID}/semelhantes`]: () => json([]),
        'GET /api/v1/atividades': () => json({ itens: [], proximo: null }),
        [`GET /api/v1/solicitacoes/${ID}/interacoes`]: () => json([]),
        ...rotas,
    })

const COMENTAR = ['solicitacao.ver', 'solicitacao.comentar']
const conversa = async () => within(await screen.findByRole('region', { name: 'Conversa' }))

describe('conversa da solicitação', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('mostra notas e mensagens, com menções pelo nome e anexos para baixar', async () => {
        const chamadas = app(['solicitacao.ver'], {
            [`GET /api/v1/solicitacoes/${ID}/interacoes`]: () =>
                json([
                    interacao({
                        autor: { id: 'k1', nome: 'Marta Souza' },
                        autorEhCliente: true,
                        visibilidade: 'EXTERNA',
                        texto: 'O forno 2 não acende.',
                        anexos: [
                            { id: 'a1', nomeArquivo: 'foto.jpg', tipoConteudo: 'image/jpeg', tamanhoBytes: 245_760 },
                        ],
                    }),
                    interacao({ texto: `@{${RAFAEL}} consegue ir hoje?` }),
                ]),
            '/api/v1/anexos/a1/download': () => json({ url: 'http://localhost:9090/acordu-anexos/x?assinatura=1' }),
        })
        await abrir(`/solicitacoes/${ID}`)
        const c = await conversa()

        const itens = await c.findAllByRole('listitem')
        expect(within(itens[0]!).getByText('Pelo portal')).toBeInTheDocument()
        expect(within(itens[0]!).getByText('cliente')).toBeInTheDocument()
        expect(c.getByText('Nota interna')).toBeInTheDocument()
        expect(c.getByText('@Rafael Moura')).toBeInTheDocument()
        // Só vê: sem caixa de mensagem.
        expect(c.queryByRole('combobox')).not.toBeInTheDocument()

        await userEvent.setup().click(c.getByRole('button', { name: /foto\.jpg/ }))
        await vi.waitFor(() => expect(chamadas.some((ch) => ch.caminho === '/api/v1/anexos/a1/download')).toBe(true))
        expect(c.getByText('240 KB')).toBeInTheDocument()
    })

    it('intercala abertura, responsável e status entre as mensagens', async () => {
        const evento = (
            tipo: string,
            minutos: number,
            detalhe: unknown = null,
            autor: unknown = { id: perfil.usuarioId, nome: 'Ana Prado' },
        ) => ({
            id: crypto.randomUUID(),
            tipo,
            ocorridaEm: daquiA(-minutos * 60_000),
            autor,
            autorEhCliente: false,
            alvo: { tipo: 'SOLICITACAO', id: ID, titulo: 'Forno não liga', numero: 1042 },
            cliente: { id: 'c1', nome: 'Padaria Bom Grão' },
            detalhe,
        })
        const chamadas = app(['solicitacao.ver'], {
            [`GET /api/v1/solicitacoes/${ID}/interacoes`]: () =>
                json([interacao({ texto: 'Vou ver isso.', registradaEm: daquiA(-30 * 60_000) })]),
            'GET /api/v1/atividades': () =>
                json({
                    itens: [
                        evento('SOLICITACAO_STATUS', 10, {
                            statusDe: 'ABERTA',
                            statusPara: 'EM_ANDAMENTO',
                            responsavel: null,
                        }),
                        evento('NOTA_INTERNA', 30),
                        evento('SOLICITACAO_ATRIBUIDA', 40, {
                            statusDe: null,
                            statusPara: null,
                            responsavel: { id: RAFAEL, nome: 'Rafael Moura' },
                        }),
                        evento('SOLICITACAO_ABERTA', 60, null, null),
                    ],
                    proximo: null,
                }),
        })
        await abrir(`/solicitacoes/${ID}`)
        const c = await conversa()

        await c.findByText(/O sistema abriu a solicitação/)
        const linhas = c.getAllByRole('listitem').map((li) => li.textContent ?? '')
        expect(linhas).toHaveLength(4)
        expect(linhas[0]).toMatch(/O sistema abriu a solicitação/)
        expect(linhas[1]).toMatch(/Ana Prado passou para Rafael Moura/)
        expect(linhas[2]).toMatch(/Vou ver isso\./)
        expect(linhas[3]).toMatch(/Ana Prado mudou para em andamento/)
        expect(chamadas.find((ch) => ch.caminho === '/api/v1/atividades')?.consulta.get('solicitacaoId')).toBe(ID)
    })

    it('registra nota interna com menção escolhida pelo teclado', async () => {
        let lista: unknown[] = []
        const chamadas = app(COMENTAR, {
            [`GET /api/v1/solicitacoes/${ID}/interacoes`]: () => json(lista),
            [`POST /api/v1/solicitacoes/${ID}/interacoes`]: (corpo) => {
                lista = [interacao(corpo as Record<string, unknown>)]
                return json(lista[0], 201)
            },
        })
        await abrir(`/solicitacoes/${ID}`)
        const c = await conversa()
        const usuario = userEvent.setup()

        const caixa = c.getByRole('combobox', { name: 'Nota interna' })
        await usuario.type(caixa, 'Veja @raf')
        expect(await c.findByRole('option', { name: 'Rafael Moura' })).toBeInTheDocument()
        await usuario.keyboard('{Enter}')
        expect(caixa).toHaveValue('Veja @Rafael Moura ')
        await usuario.type(caixa, 'isso{Control>}{Enter}{/Control}')

        await vi.waitFor(() => expect(caixa).toHaveValue(''))
        expect(chamadas.find((ch) => ch.metodo === 'POST' && ch.caminho.endsWith('/interacoes'))?.corpo).toEqual({
            texto: `Veja @{${RAFAEL}} isso`,
            visibilidade: 'INTERNA',
        })
        expect(await c.findByText('@Rafael Moura')).toBeInTheDocument()
    })

    it('responde ao cliente com anexo em multipart', async () => {
        const chamadas = app(COMENTAR, {
            [`POST /api/v1/solicitacoes/${ID}/interacoes`]: () => json(interacao({}), 201),
        })
        await abrir(`/solicitacoes/${ID}`)
        const c = await conversa()
        const usuario = userEvent.setup()

        await usuario.click(c.getByRole('radio', { name: 'Resposta ao cliente' }))
        expect(c.getByText(/Marta Souza recebe por e-mail/)).toBeInTheDocument()
        const entrada = document.querySelector<HTMLInputElement>('input[type="file"]')!
        await usuario.upload(entrada, new File(['%PDF-1.4'], 'orcamento.pdf', { type: 'application/pdf' }))
        expect(c.getByRole('list', { name: 'Arquivos para enviar' })).toHaveTextContent('orcamento.pdf')
        await usuario.type(c.getByRole('combobox', { name: 'Resposta ao cliente' }), 'Segue o orçamento.')
        await usuario.click(c.getByRole('button', { name: 'Enviar ao cliente' }))

        await vi.waitFor(() =>
            expect(chamadas.some((ch) => ch.metodo === 'POST' && ch.caminho.endsWith('/interacoes'))).toBe(true),
        )
        const corpo = chamadas.find((ch) => ch.metodo === 'POST' && ch.caminho.endsWith('/interacoes'))!
            .corpo as FormData
        expect(corpo.get('texto')).toBe('Segue o orçamento.')
        expect(corpo.get('visibilidade')).toBe('EXTERNA')
        // O File do jsdom chega ao FormData do Node sem o nome (no navegador vai como `orcamento.pdf`).
        expect(corpo.getAll('anexos')).toHaveLength(1)
        await vi.waitFor(() => expect(c.queryByRole('list', { name: 'Arquivos para enviar' })).not.toBeInTheDocument())
    })

    it('recusa arquivo acima de 20 MB antes de enviar', async () => {
        app(COMENTAR)
        await abrir(`/solicitacoes/${ID}`)
        const c = await conversa()
        const grande = new File(['x'], 'video.mp4')
        Object.defineProperty(grande, 'size', { value: 21 * 1024 * 1024 })
        await userEvent.setup().upload(document.querySelector<HTMLInputElement>('input[type="file"]')!, grande)
        expect(await c.findByText(/video\.mp4: acima de 20 MB/)).toBeInTheDocument()
        expect(c.queryByRole('list', { name: 'Arquivos para enviar' })).not.toBeInTheDocument()
    })
})
