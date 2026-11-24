import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ErroApi } from '@/shared/api/problema'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import {
    abrirSessaoOperacao,
    lerSessaoOperacao,
    reiniciarSessaoOperacaoParaTestes,
    sessaoOperacao,
} from '@/shared/sessao/sessaoOperacao'
import { abrir, daquiA, json, semSessao, servidor, type Rotas } from '@/testes/app'
import { repetirConsulta, SessaoOperacaoEncerrada } from './api'

// Spec do F14, telas da área para convites e lista de espera (F14q), sobre a API do F14n e do F14o (contrato §17).

const TOKEN = 'token-de-operacao'
const CONVITES = '/api/v1/nebrys/convites'
const LISTA = '/api/v1/nebrys/lista-espera'

const convite = {
    id: '0193c0de-0000-7000-8000-000000000001',
    email: 'ana@estudiotraco.com.br',
    situacao: 'PENDENTE',
    criadoEm: '2026-11-23T15:00:00Z',
    enviadoEm: '2026-11-23T15:00:00Z',
    expiraEm: '2026-11-30T15:00:00Z',
    usadoEm: null,
    revogadoEm: null,
}
const DETALHE = `${CONVITES}/${convite.id}`

const entrada = {
    id: '0193e5e5-0000-7000-8000-000000000001',
    nome: 'Ana Souza',
    email: 'ana@estudiotraco.com.br',
    nomeEmpresa: 'Estúdio Traço',
    mensagem: 'Organizar o atendimento dos clientes.',
    situacao: 'AGUARDANDO',
    criadaEm: '2026-11-23T15:00:00Z',
    convidadaEm: null,
    conviteId: null,
}

const pagina = (itens: unknown[]) => json({ itens, pagina: 1, tamanhoPagina: 25, total: itens.length })

const problema = (status: number, tipo: string, detail: string, errors?: Record<string, string[]>) =>
    new Response(JSON.stringify({ type: `https://acordu.app.br/erros/${tipo}`, status, title: tipo, detail, errors }), {
        status,
        headers: { 'content-type': 'application/problem+json' },
    })

function operando(rotas: Rotas = {}) {
    abrirSessaoOperacao({ accessToken: TOKEN, expiraEm: daquiA(30 * 60_000) })
    return servidor({ '/api/v1/auth/refresh': semSessao, ...rotas })
}

describe('área da Nebrys: convites e lista de espera', () => {
    beforeEach(() => {
        reiniciarParaTestes()
        reiniciarSessaoOperacaoParaTestes()
        localStorage.clear()
        sessionStorage.clear()
    })
    afterEach(() => vi.unstubAllGlobals())

    describe('casca', () => {
        it('Casca_ComSessao_TemConvitesEListaDeEspera', async () => {
            operando({ [CONVITES]: () => pagina([]) })
            await abrir('/nebrys/convites')
            const menu = await screen.findByRole('navigation', { name: 'Área da Nebrys' })
            expect(within(menu).getByRole('link', { name: 'Convites' })).toHaveAttribute('href', '/nebrys/convites')
            expect(within(menu).getByRole('link', { name: 'Lista de espera' })).toHaveAttribute(
                'href',
                '/nebrys/lista-espera',
            )
        })

        it.each(['/nebrys/convites', `/nebrys/convites/${convite.id}`, '/nebrys/lista-espera'])(
            'RotaNova_SemSessaoDeOperacao_VaiParaAEntradaSemChamarAArea (%s)',
            async (caminho) => {
                const chamadas = servidor({ '/api/v1/auth/refresh': semSessao })
                const roteador = await abrir(caminho)
                expect(await screen.findByRole('heading', { name: 'Área da Nebrys' })).toBeInTheDocument()
                expect(roteador.state.location.pathname).toBe('/nebrys')
                expect(chamadas.some((c) => c.caminho.startsWith('/api/v1/nebrys'))).toBe(false)
            },
        )
    })

    describe('convites', () => {
        it('Convites_Lista_MostraEmailSituacaoEValidadeComOBearer', async () => {
            const chamadas = operando({ [CONVITES]: () => pagina([convite]) })
            await abrir('/nebrys/convites')

            expect(await screen.findByRole('link', { name: convite.email })).toHaveAttribute(
                'href',
                `/nebrys/convites/${convite.id}`,
            )
            expect(screen.getByText('Pendente')).toBeInTheDocument()
            expect(screen.getByText('Vale até 30/11/2026, 12:00')).toBeInTheDocument()
            const pedido = chamadas.find((c) => c.caminho === CONVITES)!
            expect(pedido.consulta.get('pagina')).toBe('1')
            const autorizacao = vi
                .mocked(globalThis.fetch)
                .mock.calls.map(([r]) => r as Request)
                .find((r) => new URL(r.url).pathname === CONVITES)
                ?.headers.get('Authorization')
            expect(autorizacao).toBe(`Bearer ${TOKEN}`)
        })

        it('Convites_ListaRecusadaPelaBarreira_EncerraAOperacao', async () => {
            operando({ [CONVITES]: () => problema(404, 'nao-encontrado', 'Não encontrado.') })
            const roteador = await abrir('/nebrys/convites')
            await waitFor(() => expect(roteador.state.location.pathname).toBe('/nebrys'))
            expect(sessaoOperacao()).toBeNull()
        })

        it('NovoConvite_Envia_MandaOEmailEAvisa', async () => {
            const chamadas = operando({
                [`GET ${CONVITES}`]: () => pagina([]),
                [`POST ${CONVITES}`]: () => json({ convite, emailEnviado: true }, 201),
            })
            const usuario = userEvent.setup()
            await abrir('/nebrys/convites')
            await usuario.click(await screen.findByRole('button', { name: 'Novo convite' }))
            await usuario.type(screen.getByLabelText('E-mail'), convite.email)
            await usuario.click(screen.getByRole('button', { name: 'Enviar convite' }))

            expect(await screen.findByText(`Convite enviado para ${convite.email}.`)).toBeInTheDocument()
            expect(chamadas.find((c) => c.metodo === 'POST' && c.caminho === CONVITES)?.corpo).toEqual({
                email: convite.email,
            })
            expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
            // A lista é lida de novo.
            await waitFor(() =>
                expect(chamadas.filter((c) => c.metodo === 'GET' && c.caminho === CONVITES).length).toBe(2),
            )
        })

        it('NovoConvite_EmailNaoSaiu_AvisaParaReenviar', async () => {
            operando({
                [`GET ${CONVITES}`]: () => pagina([]),
                [`POST ${CONVITES}`]: () => json({ convite, emailEnviado: false }, 201),
            })
            const usuario = userEvent.setup()
            await abrir('/nebrys/convites')
            await usuario.click(await screen.findByRole('button', { name: 'Novo convite' }))
            await usuario.type(screen.getByLabelText('E-mail'), convite.email)
            await usuario.click(screen.getByRole('button', { name: 'Enviar convite' }))

            expect(await screen.findByRole('status')).toHaveTextContent(/o e-mail não saiu/)
        })

        it('NovoConvite_ConviteEmAberto422_MostraAExplicacaoNaJanela', async () => {
            const texto = 'Este e-mail já tem um convite em aberto: reenvie ou revogue.'
            operando({
                [`GET ${CONVITES}`]: () => pagina([convite]),
                [`POST ${CONVITES}`]: () => problema(422, 'transicao-invalida', texto),
            })
            const usuario = userEvent.setup()
            await abrir('/nebrys/convites')
            await usuario.click(await screen.findByRole('button', { name: 'Novo convite' }))
            await usuario.type(screen.getByLabelText('E-mail'), convite.email)
            await usuario.click(screen.getByRole('button', { name: 'Enviar convite' }))

            expect(await within(screen.getByRole('dialog')).findByRole('alert')).toHaveTextContent(texto)
        })

        it('NovoConvite_EmailInvalido400_MostraNoCampo', async () => {
            operando({
                [`GET ${CONVITES}`]: () => pagina([]),
                [`POST ${CONVITES}`]: () =>
                    problema(400, 'validacao', 'Dados inválidos.', { email: ['E-mail inválido.'] }),
            })
            const usuario = userEvent.setup()
            await abrir('/nebrys/convites')
            await usuario.click(await screen.findByRole('button', { name: 'Novo convite' }))
            await usuario.type(screen.getByLabelText('E-mail'), 'ana@exemplo.com')
            await usuario.click(screen.getByRole('button', { name: 'Enviar convite' }))

            expect(await screen.findByText('E-mail inválido.')).toBeInTheDocument()
            expect(screen.getByLabelText('E-mail')).toHaveAttribute('aria-invalid', 'true')
        })

        it('Detalhe_Pendente_MostraAsDatasEAsDuasAcoes', async () => {
            operando({ [DETALHE]: () => json(convite) })
            await abrir(`/nebrys/convites/${convite.id}`)
            expect(await screen.findByRole('heading', { level: 1, name: convite.email })).toBeInTheDocument()
            expect(screen.getByText('Vale até 30/11/2026, 12:00')).toBeInTheDocument()
            expect(screen.getByRole('button', { name: 'Reenviar' })).toBeInTheDocument()
            expect(screen.getByRole('button', { name: 'Revogar' })).toBeInTheDocument()
        })

        it.each(['USADO', 'REVOGADO'])('Detalhe_%s_SemAcoes', async (situacao) => {
            operando({
                [DETALHE]: () =>
                    json({
                        ...convite,
                        situacao,
                        usadoEm: situacao === 'USADO' ? '2026-11-24T10:00:00Z' : null,
                        revogadoEm: situacao === 'REVOGADO' ? '2026-11-24T10:00:00Z' : null,
                    }),
            })
            await abrir(`/nebrys/convites/${convite.id}`)
            await screen.findByRole('heading', { level: 1, name: convite.email })
            expect(screen.queryByRole('button', { name: 'Reenviar' })).not.toBeInTheDocument()
            expect(screen.queryByRole('button', { name: 'Revogar' })).not.toBeInTheDocument()
        })

        it('Reenviar_MandaOReenvioEMostraAValidadeNova', async () => {
            const novo = { ...convite, enviadoEm: '2026-11-24T15:00:00Z', expiraEm: '2026-12-01T15:00:00Z' }
            const chamadas = operando({
                [`GET ${DETALHE}`]: () => json(convite),
                [`POST ${DETALHE}/reenvio`]: () => json({ convite: novo, emailEnviado: true }),
            })
            const usuario = userEvent.setup()
            await abrir(`/nebrys/convites/${convite.id}`)
            await usuario.click(await screen.findByRole('button', { name: 'Reenviar' }))
            expect(screen.getByRole('dialog')).toHaveTextContent(/O link anterior para de valer/)
            await usuario.click(screen.getByRole('button', { name: 'Reenviar o convite' }))

            expect(await screen.findByText(`Convite reenviado para ${convite.email}.`)).toBeInTheDocument()
            expect(screen.getByText('Vale até 01/12/2026, 12:00')).toBeInTheDocument()
            expect(chamadas.filter((c) => c.caminho === `${DETALHE}/reenvio`)).toHaveLength(1)
        })

        it('Revogar_MandaARevogacaoESomemAsAcoes', async () => {
            const chamadas = operando({
                [`GET ${DETALHE}`]: () => json(convite),
                [`POST ${DETALHE}/revogacao`]: () =>
                    json({ ...convite, situacao: 'REVOGADO', revogadoEm: '2026-11-24T15:00:00Z' }),
            })
            const usuario = userEvent.setup()
            await abrir(`/nebrys/convites/${convite.id}`)
            await usuario.click(await screen.findByRole('button', { name: 'Revogar' }))
            await usuario.click(screen.getByRole('button', { name: 'Revogar o convite' }))

            expect(await screen.findByText('Convite revogado.')).toBeInTheDocument()
            expect(screen.getByText('Revogado em 24/11/2026, 12:00')).toBeInTheDocument()
            expect(screen.queryByRole('button', { name: 'Reenviar' })).not.toBeInTheDocument()
            expect(chamadas.filter((c) => c.caminho === `${DETALHE}/revogacao`)).toHaveLength(1)
        })

        it('Acao_ConviteQueMudou422_MostraAExplicacaoNaJanela', async () => {
            const texto = 'Este convite já foi usado.'
            operando({
                [`GET ${DETALHE}`]: () => json(convite),
                [`POST ${DETALHE}/revogacao`]: () => problema(422, 'transicao-invalida', texto),
            })
            const usuario = userEvent.setup()
            await abrir(`/nebrys/convites/${convite.id}`)
            await usuario.click(await screen.findByRole('button', { name: 'Revogar' }))
            await usuario.click(screen.getByRole('button', { name: 'Revogar o convite' }))

            expect(await within(screen.getByRole('dialog')).findByRole('alert')).toHaveTextContent(texto)
        })

        it('Acao_ConviteQueSumiu404_FechaEReleODetalhe', async () => {
            let leituras = 0
            operando({
                [`GET ${DETALHE}`]: () => {
                    leituras++
                    return leituras === 1 ? json(convite) : problema(404, 'nao-encontrado', 'Não encontrado.')
                },
                [`POST ${DETALHE}/reenvio`]: () => problema(404, 'nao-encontrado', 'Não encontrado.'),
            })
            const usuario = userEvent.setup()
            await abrir(`/nebrys/convites/${convite.id}`)
            await usuario.click(await screen.findByRole('button', { name: 'Reenviar' }))
            await usuario.click(screen.getByRole('button', { name: 'Reenviar o convite' }))

            expect(await screen.findByRole('heading', { level: 1, name: 'Convite não encontrado' })).toBeInTheDocument()
            expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
            // O 404 do detalhe não encerra a sessão (pode ser o convite, não a barreira).
            expect(sessaoOperacao()).not.toBeNull()
        })
    })

    describe('lista de espera', () => {
        it('Lista_MostraAsEntradasPorOrdemDeChegadaComAsAcoes', async () => {
            const convidada = {
                ...entrada,
                id: '0193e5e5-0000-7000-8000-000000000002',
                nome: 'Bia Lima',
                email: 'bia@exemplo.com',
                mensagem: null,
                situacao: 'CONVIDADA',
                convidadaEm: '2026-11-24T15:00:00Z',
                conviteId: convite.id,
            }
            operando({ [LISTA]: () => pagina([entrada, convidada]) })
            await abrir('/nebrys/lista-espera')

            const linhas = (await screen.findAllByRole('row')).slice(1)
            expect(linhas).toHaveLength(2)
            expect(linhas[0]).toHaveTextContent('Ana Souza')
            expect(linhas[0]).toHaveTextContent('Estúdio Traço')
            expect(linhas[0]).toHaveTextContent('Organizar o atendimento dos clientes.')
            expect(linhas[0]).toHaveTextContent('Aguardando')
            expect(within(linhas[0]!).getByRole('button', { name: 'Convidar Ana Souza' })).toBeInTheDocument()
            expect(linhas[1]).toHaveTextContent('Convidada')
            expect(within(linhas[1]!).queryByRole('button', { name: /Convidar/ })).not.toBeInTheDocument()
            expect(within(linhas[1]!).getByRole('button', { name: 'Descartar Bia Lima' })).toBeInTheDocument()
            expect(within(linhas[1]!).getByRole('link', { name: 'Ver o convite' })).toHaveAttribute(
                'href',
                `/nebrys/convites/${convite.id}`,
            )
        })

        it('Lista_Vazia_DizQueNinguemEsta', async () => {
            operando({ [LISTA]: () => pagina([]) })
            await abrir('/nebrys/lista-espera')
            expect(await screen.findByText('Ninguém na lista de espera')).toBeInTheDocument()
        })

        it('Lista_RecusadaPelaBarreira_EncerraAOperacao', async () => {
            operando({ [LISTA]: () => problema(404, 'nao-encontrado', 'Não encontrado.') })
            const roteador = await abrir('/nebrys/lista-espera')
            await waitFor(() => expect(roteador.state.location.pathname).toBe('/nebrys'))
            expect(sessaoOperacao()).toBeNull()
        })

        it('Convidar_MandaOConviteDaEntradaEAvisa', async () => {
            let convidou = false
            const chamadas = operando({
                [`GET ${LISTA}`]: () =>
                    pagina([
                        convidou
                            ? {
                                  ...entrada,
                                  situacao: 'CONVIDADA',
                                  convidadaEm: '2026-11-24T15:00:00Z',
                                  conviteId: convite.id,
                              }
                            : entrada,
                    ]),
                [`POST ${LISTA}/${entrada.id}/convite`]: () => {
                    convidou = true
                    return json({ convite, emailEnviado: true }, 201)
                },
            })
            const usuario = userEvent.setup()
            await abrir('/nebrys/lista-espera')
            await usuario.click(await screen.findByRole('button', { name: 'Convidar Ana Souza' }))
            expect(screen.getByRole('dialog')).toHaveTextContent(entrada.email)
            await usuario.click(screen.getByRole('button', { name: 'Enviar convite' }))

            expect(await screen.findByText(`Convite enviado para ${convite.email}.`)).toBeInTheDocument()
            expect(
                chamadas.filter((c) => c.metodo === 'POST' && c.caminho === `${LISTA}/${entrada.id}/convite`),
            ).toHaveLength(1)
            expect(await screen.findByText('Convidada')).toBeInTheDocument()
        })

        it('Convidar_EmailComConviteEmAberto422_MostraAExplicacaoNaJanela', async () => {
            const texto = 'Este e-mail já tem um convite em aberto: reenvie ou revogue.'
            operando({
                [`GET ${LISTA}`]: () => pagina([entrada]),
                [`POST ${LISTA}/${entrada.id}/convite`]: () => problema(422, 'transicao-invalida', texto),
            })
            const usuario = userEvent.setup()
            await abrir('/nebrys/lista-espera')
            await usuario.click(await screen.findByRole('button', { name: 'Convidar Ana Souza' }))
            await usuario.click(screen.getByRole('button', { name: 'Enviar convite' }))

            expect(await within(screen.getByRole('dialog')).findByRole('alert')).toHaveTextContent(texto)
        })

        it('Descartar_ApagaAEntradaEAvisa', async () => {
            let descartou = false
            const chamadas = operando({
                [`GET ${LISTA}`]: () => pagina(descartou ? [] : [entrada]),
                [`DELETE ${LISTA}/${entrada.id}`]: () => {
                    descartou = true
                    return new Response(null, { status: 204 })
                },
            })
            const usuario = userEvent.setup()
            await abrir('/nebrys/lista-espera')
            await usuario.click(await screen.findByRole('button', { name: 'Descartar Ana Souza' }))
            expect(screen.getByRole('dialog')).toHaveTextContent(/A entrada é apagada/)
            await usuario.click(screen.getByRole('button', { name: 'Descartar a entrada' }))

            expect(await screen.findByText('Entrada de Ana Souza descartada.')).toBeInTheDocument()
            expect(chamadas.filter((c) => c.metodo === 'DELETE')).toHaveLength(1)
            expect(await screen.findByText('Ninguém na lista de espera')).toBeInTheDocument()
        })

        it('Acao_AoMesmoTempo409_FechaReleALista', async () => {
            let leituras = 0
            operando({
                [`GET ${LISTA}`]: () => {
                    leituras++
                    return pagina(
                        leituras === 1
                            ? [entrada]
                            : [{ ...entrada, situacao: 'CONVIDADA', convidadaEm: '2026-11-24T15:00:00Z' }],
                    )
                },
                [`DELETE ${LISTA}/${entrada.id}`]: () => problema(409, 'conflito-versao', 'Conflito.'),
            })
            const usuario = userEvent.setup()
            await abrir('/nebrys/lista-espera')
            await usuario.click(await screen.findByRole('button', { name: 'Descartar Ana Souza' }))
            await usuario.click(screen.getByRole('button', { name: 'Descartar a entrada' }))

            expect(await screen.findByRole('status')).toHaveTextContent(/A entrada mudou/)
            expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
            expect(await screen.findByText('Convidada')).toBeInTheDocument()
        })

        it('Acao_EntradaQueSumiu404_AvisaSemEncerrarASessao', async () => {
            operando({
                [`GET ${LISTA}`]: () => pagina([entrada]),
                [`POST ${LISTA}/${entrada.id}/convite`]: () => problema(404, 'nao-encontrado', 'Não encontrado.'),
            })
            const usuario = userEvent.setup()
            await abrir('/nebrys/lista-espera')
            await usuario.click(await screen.findByRole('button', { name: 'Convidar Ana Souza' }))
            await usuario.click(screen.getByRole('button', { name: 'Enviar convite' }))

            expect(await screen.findByRole('status')).toHaveTextContent(/não existe mais/)
            expect(sessaoOperacao()).not.toBeNull()
        })
    })

    describe('apoio', () => {
        it.each([
            ['RecusaDaApi404', new ErroApi(404, null), 0, false],
            ['RecusaDaApi422', new ErroApi(422, null), 0, false],
            ['SessaoEncerrada', new SessaoOperacaoEncerrada(), 0, false],
            ['FalhaDoServidor', new ErroApi(503, null), 0, true],
            ['FalhaDeRede', new TypeError('Failed to fetch'), 0, true],
            ['SegundaFalha', new TypeError('Failed to fetch'), 1, false],
        ])('RepetirConsulta_%s_RepeteSoFalhaDeRedeOuDoServidorUmaVez', (_, erro, falhas, esperado) => {
            expect(repetirConsulta(falhas as number, erro)).toBe(esperado)
        })

        it('LerSessaoOperacao_Vencida_NaoEncerraNemAvisa', () => {
            abrirSessaoOperacao({ accessToken: TOKEN, expiraEm: daquiA(-1_000) })
            expect(lerSessaoOperacao()).not.toBeNull()
            expect(sessaoOperacao()).toBeNull()
        })
    })
})
