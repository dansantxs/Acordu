import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comContato, json } from '@/testes/app'

const solicitacao = (mudancas: Record<string, unknown> = {}) => ({
    id: 's1',
    numero: 1042,
    titulo: 'Ajustar banner da campanha',
    descricao: 'O banner de outubro está cortado no celular.',
    statusExterno: 'EM_ANDAMENTO',
    abertaEm: '2026-10-20T12:00:00Z',
    responsavel: { nome: 'Daniel' },
    sla: { prazoResolucao: '2026-10-24T18:00:00Z', situacao: 'NO_PRAZO', pausado: false },
    tarefasVisiveis: [
        { titulo: 'Refazer arte', status: 'FAZENDO' },
        { titulo: 'Briefing', status: 'CONCLUIDA' },
    ],
    tempoVisivelMinutos: 90,
    podeAprovar: false,
    encerrada: false,
    ...mudancas,
})
const interacoes = [
    {
        id: 'i1',
        texto: 'Pode mandar a arte em alta?',
        autorNome: 'Daniel',
        daEquipe: true,
        registradaEm: '2026-10-21T13:00:00Z',
        anexos: [{ id: 'a1', nomeArquivo: 'rascunho.png', tipoConteudo: 'image/png', tamanhoBytes: 2048 }],
    },
]

describe('portal: detalhe', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('mostra o andamento e a conversa, baixa pelo portal e responde', async () => {
        const chamadas = comContato({
            'GET /api/v1/portal/solicitacoes/s1': () => json(solicitacao()),
            'GET /api/v1/portal/solicitacoes/s1/interacoes': () => json(interacoes),
            'POST /api/v1/portal/solicitacoes/s1/interacoes': (corpo) =>
                json(
                    {
                        id: 'i2',
                        texto: (corpo as { texto: string }).texto,
                        autorNome: 'Marta Souza',
                        daEquipe: false,
                        registradaEm: '2026-10-22T10:00:00Z',
                        anexos: [],
                    },
                    201,
                ),
            '/api/v1/portal/anexos/a1/download': () => json({ url: 'http://localhost:9090/x?assinatura=1' }),
        })
        await abrir('/portal/solicitacoes/s1')
        const andamento = await screen.findByRole('region', { name: 'Andamento' })
        expect(within(andamento).getByText('Daniel')).toBeInTheDocument()
        expect(within(andamento).getByText('1h30')).toBeInTheDocument()
        expect(within(andamento).getByText('Refazer arte')).toBeInTheDocument()
        const usuario = userEvent.setup()

        await usuario.click(await screen.findByRole('button', { name: /rascunho\.png/ }))
        await vi.waitFor(() =>
            expect(chamadas.some((c) => c.caminho === '/api/v1/portal/anexos/a1/download')).toBe(true),
        )

        await usuario.type(screen.getByRole('textbox', { name: 'Mensagem para a equipe' }), 'Mando à tarde.')
        await usuario.click(screen.getByRole('button', { name: 'Enviar' }))
        await vi.waitFor(() =>
            expect(chamadas.find((c) => c.metodo === 'POST' && c.caminho.endsWith('/interacoes'))?.corpo).toEqual({
                texto: 'Mando à tarde.',
            }),
        )
    })

    it('aprova a entrega e pede ajuste com o motivo', async () => {
        let atual = solicitacao({ statusExterno: 'AGUARDANDO_VOCE', podeAprovar: true })
        const chamadas = comContato({
            'GET /api/v1/portal/solicitacoes/s1': () => json(atual),
            'GET /api/v1/portal/solicitacoes/s1/interacoes': () => json([]),
            'POST /api/v1/portal/solicitacoes/s1/reprovacao': () => {
                atual = solicitacao({ statusExterno: 'EM_ANDAMENTO' })
                return json(atual)
            },
            'POST /api/v1/portal/solicitacoes/s1/aprovacao': () => json(solicitacao({ statusExterno: 'CONCLUIDA' })),
        })
        await abrir('/portal/solicitacoes/s1')
        const usuario = userEvent.setup()
        const caixa = await screen.findByRole('region', { name: /A entrega está pronta/ })
        await usuario.click(within(caixa).getByRole('button', { name: 'Pedir ajuste' }))
        const janela = await screen.findByRole('dialog', { name: 'Pedir ajuste' })
        await usuario.type(within(janela).getByRole('textbox', { name: /O que precisa/ }), 'A cor ficou diferente.')
        await usuario.click(within(janela).getByRole('button', { name: 'Enviar pedido' }))
        expect(await screen.findByText('Pedido de ajuste enviado. A equipe retoma o atendimento.')).toBeInTheDocument()
        expect(chamadas.find((c) => c.caminho.endsWith('/reprovacao'))?.corpo).toEqual({
            motivo: 'A cor ficou diferente.',
        })
        await vi.waitFor(() =>
            expect(screen.queryByRole('region', { name: /A entrega está pronta/ })).not.toBeInTheDocument(),
        )
    })

    it('encerrada não tem caixa de mensagem', async () => {
        comContato({
            'GET /api/v1/portal/solicitacoes/s1': () =>
                json(solicitacao({ statusExterno: 'CONCLUIDA', sla: null, encerrada: true })),
            'GET /api/v1/portal/solicitacoes/s1/interacoes': () => json([]),
        })
        await abrir('/portal/solicitacoes/s1')
        expect(
            await screen.findByText('Esta solicitação foi encerrada. Para continuar, abra uma nova.'),
        ).toBeInTheDocument()
        expect(screen.queryByRole('textbox', { name: 'Mensagem para a equipe' })).not.toBeInTheDocument()
        expect(screen.getByText('Sem prazo combinado')).toBeInTheDocument()
        // Com conta, o atalho para a nova solicitação (F11-08).
        expect(screen.getByRole('link', { name: 'Nova solicitação' })).toHaveAttribute('href', '/portal/nova')
    })

    it('resolvida, para quem não aprova, ainda tem a caixa de mensagem (F11-10)', async () => {
        comContato({
            // RESOLVIDA para quem não aprova, como a API devolve: CONCLUIDA, sem aprovar e não encerrada.
            'GET /api/v1/portal/solicitacoes/s1': () =>
                json(solicitacao({ statusExterno: 'CONCLUIDA', sla: null, encerrada: false })),
            'GET /api/v1/portal/solicitacoes/s1/interacoes': () => json([]),
        })
        await abrir('/portal/solicitacoes/s1')
        expect(await screen.findByRole('textbox', { name: 'Mensagem para a equipe' })).toBeInTheDocument()
        expect(screen.queryByText(/Esta solicitação foi encerrada/)).not.toBeInTheDocument()
    })

    it('no link, encerrada orienta a falar com a equipe, sem botão de nova (F11-08)', async () => {
        comContato(
            {
                'GET /api/v1/portal/solicitacoes/s1': () =>
                    json(solicitacao({ statusExterno: 'CANCELADA', sla: null, encerrada: true })),
                'GET /api/v1/portal/solicitacoes/s1/interacoes': () => json([]),
            },
            'TODAS_DO_CLIENTE',
            ['portal.solicitacao.ver', 'portal.solicitacao.comentar'],
        )
        await abrir('/portal/solicitacoes/s1')
        expect(
            await screen.findByText('Esta solicitação foi encerrada. Para outro assunto, fale com a equipe.'),
        ).toBeInTheDocument()
        expect(screen.queryByRole('link', { name: 'Nova solicitação' })).not.toBeInTheDocument()
    })
})
