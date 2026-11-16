import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Tenant } from '@/shared/sessao/gravacao'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comSessao, daquiA, json, perfil, servidor, type Rotas } from '@/testes/app'

/** Situação da empresa na casca e nas ações da equipe (spec do F14: F14-10, F14-11, F14-25 a F14-27). */

const DIA = 86_400_000
const pagina = (itens: unknown[] = []) => json({ itens, pagina: 1, tamanhoPagina: 50, total: itens.length })
const semAvisos = () => json({ itens: [], pagina: 1, tamanhoPagina: 10, total: 0, naoLidas: 0 })

const EM_TESTE = (fimDoTeste: string): Partial<Tenant> => ({ situacao: 'TESTE', fimDoTeste })
const VENCIDA: Partial<Tenant> = {
    situacao: 'VENCIDA',
    fimDoTeste: '2026-11-09T15:00:00Z',
    somenteLeituraDesde: '2026-11-10T09:00:00Z',
    expurgoPrevistoEm: '2026-12-10T09:00:00Z',
}
const CANCELADA: Partial<Tenant> = {
    situacao: 'CANCELADA',
    fimDoTeste: null,
    somenteLeituraDesde: '2026-11-12T09:00:00Z',
    expurgoPrevistoEm: '2026-12-12T09:00:00Z',
}

/** Sessão da Ana com a empresa na situação dada; `tenant` pode ser trocado durante o teste (F14-27). */
function app(permissoes: string[], tenant: () => Partial<Tenant>, rotas: Rotas = {}) {
    return servidor({
        '/api/v1/auth/refresh': comSessao,
        '/api/v1/auth/eu': () => json({ ...perfil, permissoes, tenant: { ...perfil.tenant, ...tenant() } }),
        '/api/v1/notificacoes': semAvisos,
        'GET /api/v1/clientes': () => pagina(),
        'GET /api/v1/equipe': () => json([{ id: 'u1', nome: 'Ana Prado' }]),
        'GET /api/v1/clientes/etiquetas': () => json([]),
        ...rotas,
    })
}

const faixa = () => screen.queryByText(/teste do Acordu|só leitura/, { selector: 'strong' })

describe('situação da empresa na casca', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('FaixaSituacao_TesteDentroDaJanelaDeAviso_MostraDiasQueFaltamEComoAtivar (F14-10)', async () => {
        app(['cliente.ver'], () => EM_TESTE(daquiA(3 * DIA)))
        await abrir('/clientes')
        const texto = await screen.findByText(/Faltam 3 dias para o fim do teste do Acordu/)
        const caixa = texto.closest('[role="status"]') as HTMLElement
        expect(caixa).toHaveTextContent('Para continuar usando, fale com a Nebrys: contato@acordu.app.br.')
        expect(within(caixa).getByRole('link', { name: 'contato@acordu.app.br' })).toHaveAttribute(
            'href',
            'mailto:contato@acordu.app.br',
        )
    })

    it('FaixaSituacao_UltimoDiaDoTeste_DizQueTerminaHoje (F14-10)', async () => {
        app(['cliente.ver'], () => EM_TESTE(daquiA(60_000)))
        await abrir('/clientes')
        expect(await screen.findByText(/O teste do Acordu termina hoje/)).toBeInTheDocument()
    })

    it('FaixaSituacao_TesteForaDaJanelaDeAviso_NaoMostraFaixa (F14-11)', async () => {
        app(['cliente.ver'], () => EM_TESTE(daquiA(8 * DIA)))
        await abrir('/clientes')
        expect(await screen.findByRole('heading', { name: 'Clientes' })).toBeInTheDocument()
        expect(faixa()).not.toBeInTheDocument()
    })

    it('FaixaSituacao_JanelaDaSessaoDe3Dias_FaixaSoApareceDentroDela (F14-10, F14-11)', async () => {
        app(['cliente.ver'], () => ({ ...EM_TESTE(daquiA(5 * DIA)), diasFaixaTeste: 3 }))
        await abrir('/clientes')
        expect(await screen.findByRole('heading', { name: 'Clientes' })).toBeInTheDocument()
        expect(faixa()).not.toBeInTheDocument()
    })

    it('FaixaSituacao_JanelaDaSessaoDe3Dias_DentroDaJanelaMostraFaixa (F14-10)', async () => {
        app(['cliente.ver'], () => ({ ...EM_TESTE(daquiA(2 * DIA)), diasFaixaTeste: 3 }))
        await abrir('/clientes')
        expect(await screen.findByText(/Faltam 2 dias para o fim do teste do Acordu/)).toBeInTheDocument()
    })

    it('FaixaSituacao_EmpresaAtiva_NaoMostraFaixa (F14-11)', async () => {
        app(['cliente.ver'], () => ({ situacao: 'ATIVA', fimDoTeste: null }))
        await abrir('/clientes')
        expect(await screen.findByRole('heading', { name: 'Clientes' })).toBeInTheDocument()
        expect(faixa()).not.toBeInTheDocument()
    })

    it('FaixaSituacao_EmpresaVencida_ExplicaDesdeQuandoAteQuandoEComoAtivar (F14-25)', async () => {
        app(['cliente.ver'], () => VENCIDA)
        await abrir('/clientes')
        const motivo = await screen.findByText(
            'O teste terminou, e a empresa está só leitura desde 10/11/2026: dá para consultar tudo, mas não gravar.',
        )
        const caixa = motivo.closest('[role="status"]') as HTMLElement
        expect(caixa).toHaveTextContent('Os dados ficam guardados até 10/12/2026; depois disso são apagados.')
        expect(caixa).toHaveTextContent('Para ativar, fale com a Nebrys: contato@acordu.app.br.')
    })

    it('FaixaSituacao_EmpresaCancelada_ExplicaComOCancelamento (F14-25)', async () => {
        app(['cliente.ver'], () => CANCELADA)
        await abrir('/clientes')
        expect(
            await screen.findByText(
                'A conta foi cancelada, e a empresa está só leitura desde 12/11/2026: dá para consultar tudo, mas não gravar.',
            ),
        ).toBeInTheDocument()
        expect(screen.getByText(/Os dados ficam guardados até 12\/12\/2026/)).toBeInTheDocument()
    })
})

describe('ações de gravar na empresa só leitura (F14-26)', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    const MOTIVO = /só leitura desde 10\/11\/2026: dá para consultar tudo, mas não gravar/

    it('ListaClientes_EmpresaVencida_NovoClienteIndisponivelComAExplicacao', async () => {
        app(['cliente.ver', 'cliente.gerenciar'], () => VENCIDA)
        await abrir('/clientes')
        const novo = await screen.findByRole('button', { name: 'Novo cliente' })
        expect(novo).toBeDisabled()
        expect(novo).toHaveAccessibleDescription(MOTIVO)
        expect(await screen.findByRole('button', { name: 'Cadastrar o primeiro cliente' })).toBeDisabled()
    })

    it('ListaClientes_EmpresaEmTeste_NovoClienteDisponivel', async () => {
        app(['cliente.ver', 'cliente.gerenciar'], () => EM_TESTE(daquiA(20 * DIA)))
        await abrir('/clientes')
        const novo = await screen.findByRole('button', { name: 'Novo cliente' })
        expect(novo).toBeEnabled()
        expect(novo).not.toHaveAccessibleDescription()
    })

    it('TelaHoras_EmpresaVencida_LancarHorasIndisponivel', async () => {
        app(['apontamento.ver', 'apontamento.criar'], () => VENCIDA, { 'GET /api/v1/apontamentos': () => pagina() })
        await abrir('/horas')
        const lancar = await screen.findByRole('button', { name: 'Lançar horas' })
        expect(lancar).toBeDisabled()
        expect(lancar).toHaveAccessibleDescription(MOTIVO)
    })

    it('Cronometro_EmpresaVencida_IniciarIndisponivel', async () => {
        const ID = '0199f0a0-0000-7000-8000-0000000000b1'
        app(['tarefa.ver', 'apontamento.criar'], () => VENCIDA, {
            'GET /api/v1/tarefas': () => pagina(),
            'GET /api/v1/cronometro': () => new Response(null, { status: 204 }),
            [`GET /api/v1/tarefas/${ID}`]: () =>
                json({
                    id: ID,
                    versao: '3',
                    titulo: 'Trocar resistência',
                    descricao: null,
                    status: 'FAZENDO',
                    clienteId: 'c1',
                    solicitacaoId: null,
                    projetoId: null,
                    recorrenciaId: null,
                    responsavel: null,
                    prazo: null,
                    estimativaMinutos: null,
                    visivelCliente: false,
                    planejadaPara: null,
                    apontadoMinutos: 0,
                    checklist: [],
                    concluidaEm: null,
                    clienteNome: 'Padaria Bom Grão',
                    solicitacaoNumero: null,
                    projetoNome: null,
                }),
            [`GET /api/v1/tarefas/${ID}/comentarios`]: () => json([]),
            [`GET /api/v1/tarefas/${ID}/seguidores`]: () => json([]),
        })
        await abrir(`/tarefas/${ID}`)
        const iniciar = await screen.findByRole('button', { name: 'Iniciar cronômetro' })
        expect(iniciar).toBeDisabled()
        expect(iniciar).toHaveAccessibleDescription(MOTIVO)
    })

    it('SecaoEquipe_EmpresaVencida_ConvidarIndisponivel', async () => {
        app(['usuario.convidar'], () => VENCIDA, {
            'GET /api/v1/usuarios': () => json([]),
            'GET /api/v1/convites': () => json([]),
            'GET /api/v1/papeis': () => json([]),
        })
        await abrir('/configuracoes/equipe')
        const convidar = await screen.findByRole('button', { name: 'Convidar pessoa' })
        expect(convidar).toBeDisabled()
        expect(convidar).toHaveAccessibleDescription(MOTIVO)
    })

    it('PaletaComandos_EmpresaVencida_SemSecaoCriar', async () => {
        app(['cliente.ver', 'cliente.gerenciar'], () => VENCIDA)
        await abrir('/clientes')
        await screen.findByRole('button', { name: 'Novo cliente' })
        await userEvent.setup().keyboard('{Control>}k{/Control}')
        const resultados = await screen.findByRole('menu', { name: 'Resultados' })
        expect(within(resultados).queryByText('Criar')).not.toBeInTheDocument()
        expect(within(resultados).getByText('Clientes')).toBeInTheDocument()
    })
})

describe('recusa de só leitura com a tela aberta (F14-27)', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('JanelaCliente_RecusaDeSoLeitura_MostraAMensagemDaApiEAFaixaSemQuebrarATela', async () => {
        let situacao: Partial<Tenant> = EM_TESTE(daquiA(20 * DIA))
        const detalhe =
            'A empresa está só leitura (o teste venceu ou a conta foi cancelada): dá para consultar tudo, mas não gravar. Para voltar a usar, fale com a Nebrys.'
        app(['cliente.ver', 'cliente.gerenciar'], () => situacao, {
            'POST /api/v1/clientes': () => {
                // A empresa venceu com a tela aberta: a API recusa, e o perfil já vem com a situação nova.
                situacao = VENCIDA
                return json(
                    {
                        type: 'https://acordu.app.br/erros/empresa-somente-leitura',
                        title: 'Empresa só leitura',
                        status: 422,
                        detail: detalhe,
                    },
                    422,
                )
            },
        })
        await abrir('/clientes')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('button', { name: 'Novo cliente' }))
        const janela = await screen.findByRole('dialog', { name: 'Novo cliente' })
        await usuario.type(within(janela).getByRole('textbox', { name: /^Nome/ }), 'Padaria Bom Grão')
        await usuario.click(within(janela).getByRole('button', { name: 'Criar cliente' }))

        expect(await within(janela).findByText(detalhe)).toBeInTheDocument()
        // A casca relê a sessão: a faixa aparece e a ação passa a indisponível, com a janela e a lista no lugar.
        expect(await screen.findByText(/a empresa está só leitura desde 10\/11\/2026/)).toBeInTheDocument()
        expect(screen.getByRole('dialog', { name: 'Novo cliente' })).toBeInTheDocument()
        await vi.waitFor(() => expect(within(janela).getByRole('button', { name: 'Criar cliente' })).toBeDisabled())
        expect(screen.getByRole('heading', { name: 'Clientes', hidden: true })).toBeInTheDocument()
    })
})
