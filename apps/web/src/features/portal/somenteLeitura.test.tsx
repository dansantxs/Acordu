import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comContato, json } from '@/testes/app'

/** Portal de empresa só leitura (spec do F14, F14-28; recusa com a tela aberta, F14-27). */

const NEUTRA = 'No momento não é possível enviar por aqui; fale com Prado Consultoria.'

const eu = (envioDisponivel: boolean) => ({
    contato: { id: 'k1', nome: 'Marta Souza', email: 'marta@bomgrao.com' },
    cliente: { id: 'c1', nome: 'Padaria Bom Grão' },
    prestador: { nome: 'Prado Consultoria', logoUrl: null },
    escopoPortal: 'TODAS_DO_CLIENTE',
    veContratos: true,
    solicitacaoId: null,
    envioDisponivel,
})

const resumo = {
    id: 's1',
    numero: 1042,
    titulo: 'Ajustar banner da campanha',
    statusExterno: 'AGUARDANDO_VOCE',
    abertaEm: '2026-10-20T12:00:00Z',
    atualizadaEm: '2026-10-21T12:00:00Z',
}
const pagina = (itens: unknown[]) => json({ itens, pagina: 1, tamanhoPagina: 20, total: itens.length })

const detalhe = {
    id: 's1',
    numero: 1042,
    titulo: 'Ajustar banner da campanha',
    descricao: 'O banner de outubro está cortado no celular.',
    statusExterno: 'AGUARDANDO_VOCE',
    abertaEm: '2026-10-20T12:00:00Z',
    responsavel: { nome: 'Daniel' },
    sla: null,
    tarefasVisiveis: [],
    tempoVisivelMinutos: 0,
    podeAprovar: true,
    encerrada: false,
}
const interacoes = [
    {
        id: 'i1',
        texto: 'Pode conferir a arte nova?',
        autorNome: 'Daniel',
        daEquipe: true,
        registradaEm: '2026-10-21T13:00:00Z',
        anexos: [],
    },
]

function portal(envio: () => boolean) {
    return comContato({
        '/api/v1/portal/eu': () => json(eu(envio())),
        'GET /api/v1/portal/solicitacoes': () => pagina([resumo]),
        'GET /api/v1/portal/solicitacoes/s1': () => json(detalhe),
        'GET /api/v1/portal/solicitacoes/s1/interacoes': () => json(interacoes),
    })
}

describe('portal de empresa só leitura (F14-28)', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('ListaPortal_EnvioIndisponivel_MostraALista_NovaSolicitacaoIndisponivelComAMensagemNeutra', async () => {
        portal(() => false)
        await abrir('/portal')
        // Vê o que já via.
        expect(await screen.findByText('Ajustar banner da campanha')).toBeInTheDocument()
        expect(screen.getByRole('status')).toHaveTextContent(NEUTRA)
        const nova = screen.getByRole('button', { name: 'Nova solicitação' })
        expect(nova).toBeDisabled()
        expect(nova).toHaveAccessibleDescription(NEUTRA)
        expect(screen.queryByRole('link', { name: 'Nova solicitação' })).not.toBeInTheDocument()
        // A mensagem não revela a situação comercial do prestador.
        expect(document.body).not.toHaveTextContent(/venc|cancel|só leitura|teste/i)
    })

    it('DetalhePortal_EnvioIndisponivel_MostraOQueJaVia_ResponderEAprovarIndisponiveis', async () => {
        portal(() => false)
        await abrir('/portal/solicitacoes/s1')
        expect(await screen.findByText('Pode conferir a arte nova?')).toBeInTheDocument()
        expect(screen.getByText('O banner de outubro está cortado no celular.')).toBeInTheDocument()
        for (const nome of ['Aprovar entrega', 'Pedir ajuste', 'Enviar', 'Anexar']) {
            const botao = screen.getByRole('button', { name: nome })
            expect(botao).toBeDisabled()
            expect(botao).toHaveAccessibleDescription(NEUTRA)
        }
    })

    it('NovaSolicitacaoPortal_EnvioIndisponivelPeloEndereco_AbrirIndisponivel', async () => {
        portal(() => false)
        await abrir('/portal/nova')
        const abrirSolicitacao = await screen.findByRole('button', { name: 'Abrir solicitação' })
        expect(abrirSolicitacao).toBeDisabled()
        expect(abrirSolicitacao).toHaveAccessibleDescription(NEUTRA)
    })

    it('ListaPortal_EnvioDisponivel_SemMensagemENovaSolicitacaoComoLink', async () => {
        portal(() => true)
        await abrir('/portal')
        expect(await screen.findByText('Ajustar banner da campanha')).toBeInTheDocument()
        expect(screen.getByRole('link', { name: 'Nova solicitação' })).toBeInTheDocument()
        expect(screen.queryByText(NEUTRA)).not.toBeInTheDocument()
    })

    it('DetalhePortal_RecusaPortalIndisponivelComATelaAberta_MostraAMensagemSemQuebrar (F14-27)', async () => {
        let envio = true
        portal(() => envio)
        const chamadas = comContatoComRecusa(() => {
            envio = false
        })
        await abrir('/portal/solicitacoes/s1')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('button', { name: 'Aprovar entrega' }))
        // A mensagem neutra da API aparece (aviso), e a casca relê o `/portal/eu`: as ações ficam indisponíveis.
        expect((await screen.findAllByText(NEUTRA)).length).toBeGreaterThan(0)
        await vi.waitFor(() => expect(screen.getByRole('button', { name: 'Aprovar entrega' })).toBeDisabled())
        expect(screen.getByText('Pode conferir a arte nova?')).toBeInTheDocument()
        expect(chamadas.some((c) => c.metodo === 'POST' && c.caminho.endsWith('/aprovacao'))).toBe(true)
    })
})

/**
 * Acrescenta ao servidor falso do teste a aprovação recusada com `422 portal-indisponivel` (a situação mudou com a tela
 * aberta). Devolve as chamadas registradas.
 */
function comContatoComRecusa(aoRecusar: () => void) {
    const anterior = globalThis.fetch as unknown as (r: Request) => Promise<Response>
    const chamadas: { metodo: string; caminho: string }[] = []
    vi.stubGlobal('fetch', async (requisicao: Request) => {
        const caminho = new URL(requisicao.url).pathname
        chamadas.push({ metodo: requisicao.method, caminho })
        if (requisicao.method === 'POST' && caminho === '/api/v1/portal/solicitacoes/s1/aprovacao') {
            aoRecusar()
            return json(
                {
                    type: 'https://acordu.app.br/erros/portal-indisponivel',
                    title: 'Envio indisponível',
                    status: 422,
                    detail: NEUTRA,
                },
                422,
            )
        }
        return anterior(requisicao)
    })
    return chamadas
}
