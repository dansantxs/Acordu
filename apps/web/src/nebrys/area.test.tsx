import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrirSessaoOperacao, reiniciarSessaoOperacaoParaTestes, sessaoOperacao } from '@/shared/sessao/sessaoOperacao'
import { abrir, comPerfil, daquiA, json, semSessao, servidor, type Rotas } from '@/testes/app'

// Spec do F14, área da Nebrys (F14m): F14-64 e as telas sobre o que a API do F14k e do F14l entrega.

const TOKEN = 'token-de-operacao'

const empresa = {
    id: '0193a1b2-0000-7000-8000-000000000001',
    nome: 'Estúdio Traço',
    documento: '52998224725',
    situacao: 'VENCIDA',
    demonstracao: false,
    criadaEm: '2026-10-06T13:00:00Z',
    fimDoTeste: '2026-11-05T03:00:00Z',
    somenteLeituraDesde: '2026-11-06T06:40:00Z',
    canceladaEm: null,
    expurgoPrevistoEm: '2026-12-06T06:40:00Z',
    criador: { nome: 'Ana Prado', email: 'ana@estudiotraco.com.br' },
    pessoasNaEquipe: 3,
    versao: '1043',
}
const { versao: _versao, ...resumo } = empresa
void _versao
const DETALHE = `/api/v1/nebrys/empresas/${empresa.id}`

/** A empresa de demonstração (#225): ATIVA, sem fim de teste nem documento, com a marca. */
const demonstracao = {
    ...empresa,
    id: '0193a1b2-0000-7000-8000-0000000000de',
    nome: 'Estúdio Demonstração',
    documento: null,
    situacao: 'ATIVA',
    demonstracao: true,
    fimDoTeste: null,
    somenteLeituraDesde: null,
    expurgoPrevistoEm: null,
}
const { versao: _versaoDemonstracao, ...resumoDemonstracao } = demonstracao
void _versaoDemonstracao

const lista = (itens: unknown[] = [resumo]) => json({ itens, pagina: 1, tamanhoPagina: 25, total: itens.length })

const problema = (status: number, tipo: string, detail: string) =>
    new Response(JSON.stringify({ type: `https://acordu.app.br/erros/${tipo}`, status, title: tipo, detail }), {
        status,
        headers: { 'content-type': 'application/problem+json' },
    })

/** Sem sessão da equipe (refresh 401) e com as rotas da área dadas. */
function semEmpresa(rotas: Rotas = {}) {
    return servidor({ '/api/v1/auth/refresh': semSessao, ...rotas })
}

function operando() {
    abrirSessaoOperacao({ accessToken: TOKEN, expiraEm: daquiA(30 * 60_000) })
}

/** Authorization de cada chamada ao fetch simulado, pelo caminho. */
function autorizacoes(caminho: string): (string | null)[] {
    return vi
        .mocked(globalThis.fetch)
        .mock.calls.map(([r]) => r as Request)
        .filter((r) => new URL(r.url).pathname === caminho)
        .map((r) => r.headers.get('Authorization'))
}

describe('área da Nebrys', () => {
    beforeEach(() => {
        reiniciarParaTestes()
        reiniciarSessaoOperacaoParaTestes()
        localStorage.clear()
        sessionStorage.clear()
    })
    afterEach(() => vi.unstubAllGlobals())

    describe('F14-64: quem não opera não vê a área', () => {
        it.each(['/nebrys', '/nebrys/empresas', `/nebrys/empresas/${empresa.id}`, '/nebrys/documentos'])(
            'Rota_SessaoDaEmpresa_NaoEncontrado (%s)',
            async (caminho) => {
                const chamadas = comPerfil(['cliente.ver'])
                await abrir(caminho)
                expect(await screen.findByText('Página não encontrada.')).toBeInTheDocument()
                expect(screen.queryByRole('heading', { name: 'Área da Nebrys' })).not.toBeInTheDocument()
                expect(screen.queryByRole('heading', { name: 'Empresas' })).not.toBeInTheDocument()
                expect(chamadas.some((c) => c.caminho.startsWith('/api/v1/nebrys'))).toBe(false)
            },
        )

        it('Rota_SessaoDaEmpresaComSessaoDeOperacaoAberta_EncerraAOperacaoENaoEncontrado', async () => {
            operando()
            const chamadas = comPerfil([])
            await abrir('/nebrys/empresas')
            expect(await screen.findByText('Página não encontrada.')).toBeInTheDocument()
            expect(sessaoOperacao()).toBeNull()
            expect(chamadas.some((c) => c.caminho.startsWith('/api/v1/nebrys'))).toBe(false)
        })

        it('RotaInterna_SemSessaoDeOperacao_VaiParaAEntradaSemChamarAArea', async () => {
            const chamadas = semEmpresa()
            const roteador = await abrir(`/nebrys/empresas/${empresa.id}`)
            expect(await screen.findByRole('heading', { name: 'Área da Nebrys' })).toBeInTheDocument()
            expect(roteador.state.location.pathname).toBe('/nebrys')
            expect(screen.queryByText('Estúdio Traço')).not.toBeInTheDocument()
            expect(chamadas.some((c) => c.caminho.startsWith('/api/v1/nebrys'))).toBe(false)
        })

        it('Entrada_SemLinkParaCadastroSenhaOuApp', async () => {
            semEmpresa()
            await abrir('/nebrys')
            await screen.findByRole('heading', { name: 'Área da Nebrys' })
            expect(screen.queryByRole('link')).not.toBeInTheDocument()
        })
    })

    describe('entrada', () => {
        it('Entrar_CredencialAceita_SessaoSoEmMemoriaEListaComOBearer', async () => {
            const chamadas = semEmpresa({
                'POST /api/v1/nebrys/sessao': () =>
                    json({ accessToken: TOKEN, expiraEm: daquiA(30 * 60_000), tipo: 'OPERACAO' }),
                '/api/v1/nebrys/empresas': () => lista(),
            })
            const usuario = userEvent.setup()
            const roteador = await abrir('/nebrys')
            await usuario.type(await screen.findByLabelText('E-mail'), 'operacao@nebrys.com.br')
            await usuario.type(screen.getByLabelText('Senha', { exact: true }), 'umasenhaboa')
            await usuario.click(screen.getByRole('button', { name: 'Entrar' }))

            expect(await screen.findByRole('link', { name: 'Estúdio Traço' })).toBeInTheDocument()
            expect(roteador.state.location.pathname).toBe('/nebrys/empresas')
            expect(chamadas.find((c) => c.caminho === '/api/v1/nebrys/sessao')?.corpo).toEqual({
                email: 'operacao@nebrys.com.br',
                senha: 'umasenhaboa',
            })
            expect(autorizacoes('/api/v1/nebrys/sessao')).toEqual([null])
            expect(autorizacoes('/api/v1/nebrys/empresas')).toEqual([`Bearer ${TOKEN}`])
            // Só memória: nada no armazenamento do navegador nem no endereço.
            expect(localStorage.length).toBe(0)
            expect(sessionStorage.length).toBe(0)
            expect(roteador.state.location.href).not.toContain(TOKEN)
        })

        it('Entrar_Recusada404_MensagemSemDizerOMotivo', async () => {
            semEmpresa({ 'POST /api/v1/nebrys/sessao': () => problema(404, 'nao-encontrado', 'Não encontrado.') })
            const usuario = userEvent.setup()
            await abrir('/nebrys')
            await usuario.type(await screen.findByLabelText('E-mail'), 'ana@exemplo.com')
            await usuario.type(screen.getByLabelText('Senha', { exact: true }), 'qualquercoisa')
            await usuario.click(screen.getByRole('button', { name: 'Entrar' }))
            expect(await screen.findByRole('alert')).toHaveTextContent(
                'Não deu para entrar. Confira o e-mail e a senha.',
            )
            expect(sessaoOperacao()).toBeNull()
        })

        it('Area_SessaoVencida_VoltaParaAEntradaComOAviso', async () => {
            abrirSessaoOperacao({ accessToken: TOKEN, expiraEm: daquiA(-1000) })
            const chamadas = semEmpresa()
            await abrir('/nebrys/empresas')
            expect(
                await screen.findByText('A sessão da área acabou. Entre de novo para continuar.'),
            ).toBeInTheDocument()
            expect(chamadas.some((c) => c.caminho.startsWith('/api/v1/nebrys'))).toBe(false)
        })

        it('Lista_RecusadaPelaBarreira_EncerraEVoltaParaAEntrada', async () => {
            operando()
            semEmpresa({ '/api/v1/nebrys/empresas': () => problema(404, 'nao-encontrado', 'Não encontrado.') })
            const roteador = await abrir('/nebrys/empresas')
            expect(await screen.findByRole('heading', { name: 'Área da Nebrys' })).toBeInTheDocument()
            expect(roteador.state.location.pathname).toBe('/nebrys')
            expect(sessaoOperacao()).toBeNull()
        })

        it('Sair_EncerraASessaoEVoltaParaAEntrada', async () => {
            operando()
            semEmpresa({ '/api/v1/nebrys/empresas': () => lista() })
            const usuario = userEvent.setup()
            await abrir('/nebrys/empresas')
            await usuario.click(await screen.findByRole('button', { name: 'Sair' }))
            expect(await screen.findByRole('heading', { name: 'Área da Nebrys' })).toBeInTheDocument()
            expect(sessaoOperacao()).toBeNull()
        })
    })

    describe('empresas', () => {
        it('Lista_MostraOsCamposDaRelacao', async () => {
            operando()
            semEmpresa({ '/api/v1/nebrys/empresas': () => lista() })
            await abrir('/nebrys/empresas')
            const linha = (await screen.findByRole('link', { name: 'Estúdio Traço' })).closest('tr')!
            const celulas = within(linha)
            expect(celulas.getByText('529.982.247-25')).toBeInTheDocument()
            expect(celulas.getByText('Vencida')).toBeInTheDocument()
            expect(celulas.getByText('Venceu em 06/11/2026')).toBeInTheDocument()
            expect(celulas.getByText('06/10/2026')).toBeInTheDocument()
            expect(celulas.getByText('06/12/2026')).toBeInTheDocument()
            expect(celulas.getByText('Ana Prado')).toBeInTheDocument()
            expect(celulas.getByText('ana@estudiotraco.com.br')).toBeInTheDocument()
            expect(celulas.getByText('3')).toBeInTheDocument()
        })

        it('Lista_FiltroPorSituacao_PedeASituacaoEGuardaNoEndereco', async () => {
            operando()
            const chamadas = semEmpresa({ '/api/v1/nebrys/empresas': () => lista() })
            const usuario = userEvent.setup()
            const roteador = await abrir('/nebrys/empresas')
            await screen.findByRole('link', { name: 'Estúdio Traço' })
            await usuario.click(screen.getByRole('tab', { name: 'Vencidas' }))
            await waitFor(() => expect(chamadas.some((c) => c.consulta.get('situacao') === 'VENCIDA')).toBe(true))
            expect(roteador.state.location.search).toMatchObject({ fase: 'VENCIDA' })
        })

        it('Detalhe_SemDadoDeNegocio_SoOsCamposDaRelacaoEAsAcoesDaSituacao', async () => {
            operando()
            semEmpresa({ [DETALHE]: () => json(empresa) })
            await abrir(`/nebrys/empresas/${empresa.id}`)
            expect(await screen.findByRole('heading', { name: 'Estúdio Traço' })).toBeInTheDocument()
            expect(screen.getByRole('button', { name: 'Ativar' })).toBeInTheDocument()
            expect(screen.getByRole('button', { name: 'Prorrogar o teste' })).toBeInTheDocument()
            expect(screen.getByRole('button', { name: 'Cancelar' })).toBeInTheDocument()
            expect(screen.queryByRole('button', { name: 'Reativar' })).not.toBeInTheDocument()
            expect(screen.queryByText(/clientes|solicitações|tarefas|contratos/i)).not.toBeInTheDocument()
        })

        it('Lista_Demonstracao_EtiquetaSoNaDemonstracao', async () => {
            operando()
            semEmpresa({ '/api/v1/nebrys/empresas': () => lista([resumoDemonstracao, resumo]) })
            await abrir('/nebrys/empresas')
            const linhaDemonstracao = (await screen.findByRole('link', { name: 'Estúdio Demonstração' })).closest('tr')!
            const linhaComum = screen.getByRole('link', { name: 'Estúdio Traço' }).closest('tr')!
            expect(within(linhaDemonstracao).getByText('Demonstração')).toBeInTheDocument()
            expect(within(linhaDemonstracao).getByText('Ativa')).toBeInTheDocument()
            expect(within(linhaComum).queryByText('Demonstração')).not.toBeInTheDocument()
        })

        it('Detalhe_Demonstracao_EtiquetaSemAsAcoesELinkParaADemonstracao', async () => {
            operando()
            const chamadas = semEmpresa({ [`/api/v1/nebrys/empresas/${demonstracao.id}`]: () => json(demonstracao) })
            await abrir(`/nebrys/empresas/${demonstracao.id}`)
            expect(await screen.findByRole('heading', { name: 'Estúdio Demonstração' })).toBeInTheDocument()
            expect(screen.getByText('Demonstração', { selector: 'span' })).toBeInTheDocument()
            for (const acao of ['Ativar', 'Prorrogar o teste', 'Cancelar', 'Reativar'])
                expect(screen.queryByRole('button', { name: acao })).not.toBeInTheDocument()
            expect(screen.getByRole('link', { name: 'Abrir a demonstração' })).toHaveAttribute(
                'href',
                '/nebrys/demonstracao',
            )
            expect(chamadas.some((c) => c.metodo !== 'GET' && c.caminho.startsWith('/api/v1/nebrys'))).toBe(false)
        })

        it('Detalhe_EmpresaComum_SemEtiquetaComAsAcoes', async () => {
            operando()
            semEmpresa({ [DETALHE]: () => json({ ...empresa, situacao: 'ATIVA', somenteLeituraDesde: null }) })
            await abrir(`/nebrys/empresas/${empresa.id}`)
            expect(await screen.findByRole('heading', { name: 'Estúdio Traço' })).toBeInTheDocument()
            expect(screen.getByRole('button', { name: 'Cancelar' })).toBeInTheDocument()
            expect(screen.queryByText('Demonstração', { selector: 'span' })).not.toBeInTheDocument()
            expect(screen.queryByRole('link', { name: 'Abrir a demonstração' })).not.toBeInTheDocument()
        })

        it('Ativar_MandaOIfMatchDaVersaoEMostraASituacaoNova', async () => {
            operando()
            const chamadas = semEmpresa({
                [DETALHE]: () => json(empresa),
                [`POST ${DETALHE}/ativacao`]: () =>
                    json({
                        ...empresa,
                        situacao: 'ATIVA',
                        fimDoTeste: null,
                        somenteLeituraDesde: null,
                        expurgoPrevistoEm: null,
                        versao: '1051',
                    }),
            })
            const usuario = userEvent.setup()
            await abrir(`/nebrys/empresas/${empresa.id}`)
            await usuario.click(await screen.findByRole('button', { name: 'Ativar' }))
            await usuario.click(await screen.findByRole('button', { name: 'Ativar a empresa' }))

            expect(await screen.findByText('Estúdio Traço: ativa.')).toBeInTheDocument()
            expect(chamadas.find((c) => c.caminho === `${DETALHE}/ativacao`)?.ifMatch).toBe('"1043"')
            expect(screen.getByRole('button', { name: 'Cancelar' })).toBeInTheDocument()
            expect(screen.queryByRole('button', { name: 'Ativar' })).not.toBeInTheDocument()
        })

        it('Acao_409VersaoDivergente_RecarregaAEmpresaEAvisa', async () => {
            operando()
            let leituras = 0
            const chamadas = semEmpresa({
                [DETALHE]: () => {
                    leituras++
                    return json(
                        leituras === 1
                            ? { ...empresa, situacao: 'TESTE', somenteLeituraDesde: null, expurgoPrevistoEm: null }
                            : { ...empresa, versao: '1044' },
                    )
                },
                [`POST ${DETALHE}/cancelamento`]: () =>
                    problema(409, 'conflito-versao', 'O registro mudou desde a leitura.'),
            })
            const usuario = userEvent.setup()
            await abrir(`/nebrys/empresas/${empresa.id}`)
            await usuario.click(await screen.findByRole('button', { name: 'Cancelar' }))
            await usuario.click(await screen.findByRole('button', { name: 'Cancelar a empresa' }))

            expect(await screen.findByText(/A empresa mudou desde que você abriu esta tela/)).toBeInTheDocument()
            await waitFor(() => expect(screen.getByText('Venceu em 06/11/2026')).toBeInTheDocument())
            expect(chamadas.filter((c) => c.metodo === 'GET' && c.caminho === DETALHE)).toHaveLength(2)
            expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
        })

        it('Acao_422TransicaoInvalida_MostraAExplicacaoDaApi', async () => {
            operando()
            const explicacao = 'A empresa de demonstração fica fora do ciclo de situação.'
            semEmpresa({
                [DETALHE]: () => json(empresa),
                [`POST ${DETALHE}/ativacao`]: () => problema(422, 'transicao-invalida', explicacao),
            })
            const usuario = userEvent.setup()
            await abrir(`/nebrys/empresas/${empresa.id}`)
            await usuario.click(await screen.findByRole('button', { name: 'Ativar' }))
            await usuario.click(await screen.findByRole('button', { name: 'Ativar a empresa' }))

            const janela = await screen.findByRole('dialog')
            expect(await within(janela).findByRole('alert')).toHaveTextContent(explicacao)
        })

        it('Acao_404EmpresaExpurgada_DizQueNaoExisteMais', async () => {
            operando()
            let leituras = 0
            semEmpresa({
                [DETALHE]: () =>
                    ++leituras === 1 ? json(empresa) : problema(404, 'nao-encontrado', 'Não encontrado.'),
                [`POST ${DETALHE}/cancelamento`]: () => problema(404, 'nao-encontrado', 'Não encontrado.'),
            })
            const usuario = userEvent.setup()
            await abrir(`/nebrys/empresas/${empresa.id}`)
            await usuario.click(await screen.findByRole('button', { name: 'Cancelar' }))
            await usuario.click(await screen.findByRole('button', { name: 'Cancelar a empresa' }))
            expect(
                await screen.findByText('Esta empresa não existe mais: pode ter sido expurgada.'),
            ).toBeInTheDocument()
        })

        it('Prorrogar_MandaADataEOIfMatch', async () => {
            operando()
            const chamadas = semEmpresa({
                [DETALHE]: () => json(empresa),
                [`POST ${DETALHE}/prorrogacao`]: () =>
                    json({ ...empresa, situacao: 'TESTE', fimDoTeste: '2026-12-16T03:00:00Z', versao: '1051' }),
            })
            const usuario = userEvent.setup()
            await abrir(`/nebrys/empresas/${empresa.id}`)
            await usuario.click(await screen.findByRole('button', { name: 'Prorrogar o teste' }))
            fireEvent.change(await screen.findByLabelText('Último dia do teste'), { target: { value: '2026-12-15' } })
            await usuario.click(screen.getByRole('button', { name: 'Prorrogar' }))

            expect(await screen.findByText('Teste até 15/12/2026')).toBeInTheDocument()
            const pedido = chamadas.find((c) => c.caminho === `${DETALHE}/prorrogacao`)
            expect(pedido?.corpo).toEqual({ ate: '2026-12-15' })
            expect(pedido?.ifMatch).toBe('"1043"')
        })

        it('Prorrogar_DiaPassado422_MostraAExplicacaoNaJanela', async () => {
            operando()
            const explicacao = 'O novo fim do teste precisa ser hoje ou depois.'
            semEmpresa({
                [DETALHE]: () => json(empresa),
                [`POST ${DETALHE}/prorrogacao`]: () => problema(422, 'transicao-invalida', explicacao),
            })
            const usuario = userEvent.setup()
            await abrir(`/nebrys/empresas/${empresa.id}`)
            await usuario.click(await screen.findByRole('button', { name: 'Prorrogar o teste' }))
            fireEvent.change(await screen.findByLabelText('Último dia do teste'), { target: { value: '2026-01-01' } })
            await usuario.click(screen.getByRole('button', { name: 'Prorrogar' }))
            expect(await within(screen.getByRole('dialog')).findByRole('alert')).toHaveTextContent(explicacao)
        })
    })

    describe('liberação de documento', () => {
        it('Liberar_204_ConfirmaEEsvaziaOCampoSemGuardarODocumento', async () => {
            operando()
            const chamadas = semEmpresa({
                'POST /api/v1/nebrys/documentos/liberacao': () => new Response(null, { status: 204 }),
            })
            const usuario = userEvent.setup()
            const roteador = await abrir('/nebrys/documentos')
            await usuario.type(await screen.findByLabelText('CPF ou CNPJ'), '52998224725')
            await usuario.click(screen.getByRole('button', { name: 'Liberar' }))

            expect(await screen.findByText('Documento liberado. O próximo cadastro com ele vai passar.')).toBeVisible()
            expect(chamadas.find((c) => c.caminho === '/api/v1/nebrys/documentos/liberacao')?.corpo).toEqual({
                documento: '529.982.247-25',
            })
            expect(autorizacoes('/api/v1/nebrys/documentos/liberacao')).toEqual([`Bearer ${TOKEN}`])
            expect(screen.getByLabelText('CPF ou CNPJ')).toHaveValue('')
            expect(roteador.state.location.href).not.toMatch(/52998224725|529\.982/)
            expect(localStorage.length).toBe(0)
            expect(sessionStorage.length).toBe(0)
        })

        it('Liberar_422_MostraAExplicacaoDaApi', async () => {
            operando()
            const explicacao = 'Este documento já tem uma liberação que ainda não foi usada.'
            semEmpresa({
                'POST /api/v1/nebrys/documentos/liberacao': () => problema(422, 'transicao-invalida', explicacao),
            })
            const usuario = userEvent.setup()
            await abrir('/nebrys/documentos')
            await usuario.type(await screen.findByLabelText('CPF ou CNPJ'), '52998224725')
            await usuario.click(screen.getByRole('button', { name: 'Liberar' }))
            expect(await screen.findByRole('alert')).toHaveTextContent(explicacao)
        })

        it('Liberar_DocumentoInvalido_NaoEnvia', async () => {
            operando()
            const chamadas = semEmpresa()
            const usuario = userEvent.setup()
            await abrir('/nebrys/documentos')
            await usuario.type(await screen.findByLabelText('CPF ou CNPJ'), '52998224724')
            await usuario.click(screen.getByRole('button', { name: 'Liberar' }))
            expect(await screen.findByText('CPF ou CNPJ inválido.')).toBeInTheDocument()
            expect(chamadas.some((c) => c.caminho === '/api/v1/nebrys/documentos/liberacao')).toBe(false)
        })
    })
})
