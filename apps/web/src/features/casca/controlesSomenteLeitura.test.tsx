import { fireEvent, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { hojeNoFuso, segundaDaSemana, somarDias } from '@/shared/format/datas'
import type { Tenant } from '@/shared/sessao/gravacao'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comSessao, daquiA, json, perfil, servidor, type Rotas } from '@/testes/app'

/**
 * Controles fora do `<Botao grava>` na empresa só leitura (#191, depois do F14e): menus "⋯", campos que salvam ao
 * mudar, botões de texto e o arrastar ficam indisponíveis com a mesma explicação da casca; o que é da pessoa (seguir,
 * sino) segue disponível.
 */

const DIA = 86_400_000
const MOTIVO = /só leitura desde 10\/11\/2026: dá para consultar tudo, mas não gravar/
const VENCIDA: Partial<Tenant> = {
    situacao: 'VENCIDA',
    fimDoTeste: '2026-11-09T15:00:00Z',
    somenteLeituraDesde: '2026-11-10T09:00:00Z',
    expurgoPrevistoEm: '2026-12-10T09:00:00Z',
}
const EM_TESTE: Partial<Tenant> = { situacao: 'TESTE', fimDoTeste: daquiA(20 * DIA) }

const ID = '0199f0a0-0000-7000-8000-0000000000b1'
const tarefa = {
    id: ID,
    versao: '3',
    titulo: 'Trocar resistência',
    descricao: null,
    status: 'A_FAZER',
    clienteId: 'c1',
    solicitacaoId: null,
    projetoId: null,
    recorrenciaId: null,
    responsavel: null,
    prazo: '2026-10-25',
    estimativaMinutos: 60,
    visivelCliente: false,
    planejadaPara: null,
    apontadoMinutos: 0,
    checklist: [{ id: 'i1', ordem: 1, texto: 'Comprar peça', feito: false }],
    concluidaEm: null,
    clienteNome: 'Padaria Bom Grão',
    solicitacaoNumero: null,
    projetoNome: null,
}
const resumo = {
    id: ID,
    titulo: 'Trocar resistência',
    status: 'A_FAZER',
    clienteId: 'c1',
    solicitacaoId: null,
    projetoId: null,
    responsavel: null,
    prazo: null,
    estimativaMinutos: null,
    visivelCliente: false,
    planejadaPara: null,
    clienteNome: 'Padaria Bom Grão',
    solicitacaoNumero: null,
    projetoNome: null,
    concluidaEm: null,
}
const EDITAR = ['tarefa.ver', 'tarefa.editar']

/** Sessão da Ana com a empresa na situação dada. */
function app(permissoes: string[], tenant: Partial<Tenant>, rotas: Rotas = {}) {
    return servidor({
        '/api/v1/auth/refresh': comSessao,
        '/api/v1/auth/eu': () => json({ ...perfil, permissoes, tenant: { ...perfil.tenant, ...tenant } }),
        '/api/v1/notificacoes': () => json({ itens: [], pagina: 1, tamanhoPagina: 10, total: 0, naoLidas: 0 }),
        'GET /api/v1/equipe': () => json([{ id: 'u1', nome: 'Ana Prado' }]),
        'GET /api/v1/tarefas': () => json({ itens: [resumo], pagina: 1, tamanhoPagina: 50, total: 1 }),
        [`GET /api/v1/tarefas/${ID}`]: () => json(tarefa),
        [`GET /api/v1/tarefas/${ID}/comentarios`]: () => json([]),
        [`GET /api/v1/tarefas/${ID}/seguidores`]: () => json([]),
        ...rotas,
    })
}

const painel = () => screen.findByRole('complementary', { name: 'Trocar resistência' })

describe('controles que gravam na empresa só leitura (#191)', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('MenuAcoes_QuadroEmpresaVencida_MoverIndisponivelComAExplicacaoESemArrastar', async () => {
        const chamadas = app(EDITAR, VENCIDA)
        await abrir('/tarefas?modo=quadro')
        const mover = await screen.findByRole('button', { name: 'Mover Trocar resistência' })
        expect(mover).toBeDisabled()
        expect(mover).toHaveAccessibleDescription(MOTIVO)
        expect(screen.queryByRole('button', { name: 'Arrastar Trocar resistência' })).not.toBeInTheDocument()

        await userEvent.setup().click(mover)
        expect(screen.queryByRole('menu', { name: 'Mover para' })).not.toBeInTheDocument()
        expect(chamadas.filter((c) => c.caminho.startsWith('/api/v1/tarefas/'))).toHaveLength(0)
    })

    it('MenuAcoes_QuadroEmpresaEmTeste_MoverEArrastarDisponiveis', async () => {
        const chamadas = app(EDITAR, EM_TESTE, {
            [`POST /api/v1/tarefas/${ID}/inicio`]: () => json({ ...tarefa, status: 'FAZENDO' }),
        })
        await abrir('/tarefas?modo=quadro')
        const mover = await screen.findByRole('button', { name: 'Mover Trocar resistência' })
        expect(mover).toBeEnabled()
        expect(mover).not.toHaveAccessibleDescription()
        expect(screen.getByRole('button', { name: 'Arrastar Trocar resistência' })).toBeInTheDocument()

        const usuario = userEvent.setup()
        await usuario.click(mover)
        await usuario.click(await screen.findByRole('menuitem', { name: 'Mover para fazendo' }))
        await vi.waitFor(() =>
            expect(chamadas.some((c) => c.metodo === 'POST' && c.caminho.endsWith('/inicio'))).toBe(true),
        )
    })

    it('MenuAcoes_PlanoDaSemanaEmpresaVencida_MoverIndisponivelComAExplicacaoESemArrastar', async () => {
        const seg = segundaDaSemana(hojeNoFuso('America/Sao_Paulo'))
        const dia = (n: number) => somarDias(seg, n)
        app(EDITAR, VENCIDA, {
            'GET /api/v1/agenda': () =>
                json({
                    de: seg,
                    ate: dia(13),
                    hoje: dia(2),
                    dias: Array.from({ length: 14 }, (_, i) => ({ data: dia(i), util: i % 7 < 5, feriado: false })),
                    equipe: [{ id: 'u1', nome: 'Ana Prado' }],
                    itens: [
                        {
                            tipo: 'TAREFA',
                            id: 't9',
                            titulo: 'Arte do cardápio',
                            inicio: dia(2),
                            fim: dia(2),
                            situacao: 'NO_PRAZO',
                            responsavel: { id: 'u1', nome: 'Ana Prado' },
                            cliente: null,
                        },
                    ],
                }),
        })
        await abrir('/agenda?vista=plano')
        const plano = await screen.findByRole('table', { name: 'Plano da semana' })
        const mover = await within(plano).findByRole('button', { name: 'Mover Arte do cardápio' })
        expect(mover).toBeDisabled()
        expect(mover).toHaveAccessibleDescription(MOTIVO)
        expect(within(plano).queryByRole('button', { name: 'Arrastar Arte do cardápio' })).not.toBeInTheDocument()
    })

    it('PainelTarefa_EmpresaVencida_CamposQueSalvamAoMudarIndisponiveisComAExplicacao', async () => {
        app(EDITAR, VENCIDA)
        await abrir(`/tarefas/${ID}`)
        const aside = await painel()
        const campos = [
            within(aside).getByRole('combobox', { name: 'Responsável' }),
            within(aside).getByLabelText('Prazo'),
            within(aside).getByLabelText('Planejada para'),
            within(aside).getByLabelText('Estimativa'),
            within(aside).getByRole('switch', { name: 'Só a equipe vê' }),
            within(aside).getByRole('checkbox', { name: 'Comprar peça' }),
            within(aside).getByRole('textbox', { name: 'Novo item da checklist' }),
        ]
        for (const campo of campos) {
            expect(campo).toBeDisabled()
            expect(campo).toHaveAccessibleDescription(MOTIVO)
        }
        // A descrição própria do campo continua junto da explicação.
        expect(within(aside).getByLabelText('Estimativa')).toHaveAccessibleDescription(/Ex\.: 1h30/)
    })

    it('BotaoTexto_PainelTarefaEmpresaVencida_TirarItemECancelarIndisponiveisComAExplicacao', async () => {
        app(EDITAR, VENCIDA)
        await abrir(`/tarefas/${ID}`)
        const aside = await painel()
        for (const nome of ['Tirar "Comprar peça"', 'Cancelar tarefa']) {
            const botao = within(aside).getByRole('button', { name: nome })
            expect(botao).toBeDisabled()
            expect(botao).toHaveAccessibleDescription(MOTIVO)
        }
    })

    it('PainelTarefa_EmpresaVencida_SeguirESinoDaPessoaSeguemDisponiveis', async () => {
        app(EDITAR, VENCIDA)
        await abrir(`/tarefas/${ID}`)
        const aside = await painel()
        const seguir = await within(aside).findByRole('button', { name: 'Seguir' })
        expect(seguir).toBeEnabled()
        expect(seguir).not.toHaveAccessibleDescription()
        expect(screen.getByRole('button', { name: 'Avisos' })).toBeEnabled()
    })

    it('PainelTarefa_EmpresaEmTeste_CamposDisponiveisESalvamAoMudar', async () => {
        const chamadas = app(EDITAR, EM_TESTE, {
            [`PATCH /api/v1/tarefas/${ID}`]: (corpo) => json({ ...tarefa, ...(corpo as object), versao: '4' }),
        })
        await abrir(`/tarefas/${ID}`)
        const aside = await painel()
        expect(within(aside).getByRole('combobox', { name: 'Responsável' })).toBeEnabled()
        expect(within(aside).getByRole('switch', { name: 'Só a equipe vê' })).toBeEnabled()
        expect(within(aside).getByRole('checkbox', { name: 'Comprar peça' })).toBeEnabled()
        expect(within(aside).getByRole('button', { name: 'Cancelar tarefa' })).toBeEnabled()
        const prazo = within(aside).getByLabelText('Prazo')
        expect(prazo).toBeEnabled()
        expect(prazo).not.toHaveAccessibleDescription()

        fireEvent.change(prazo, { target: { value: '2026-10-30' } })
        await vi.waitFor(() =>
            expect(chamadas.find((c) => c.metodo === 'PATCH')?.corpo).toMatchObject({ prazo: '2026-10-30' }),
        )
    })
})

describe('links de texto da ficha do cliente na empresa só leitura (#191)', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    const C = 'c1'
    const cliente = {
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
        contatos: [
            {
                id: 'k1',
                nome: 'Marta Souza',
                email: 'marta@bomgrao.com',
                telefone: null,
                cargo: null,
                ativo: true,
                temConta: false,
                acessoPortal: null,
            },
        ],
    }
    const PERMISSOES = ['cliente.ver', 'cliente.gerenciar', 'portal.gerenciar_acessos']

    it('BotaoTexto_FichaContatoEmpresaVencida_ConvidarEditarEInativarIndisponiveisComAExplicacao', async () => {
        app(PERMISSOES, VENCIDA, { [`GET /api/v1/clientes/${C}`]: () => json(cliente) })
        await abrir(`/clientes/${C}?secao=contatos`)
        for (const nome of ['Convidar Marta Souza para o portal', 'Editar Marta Souza', 'Inativar Marta Souza']) {
            const botao = await screen.findByRole('button', { name: nome })
            expect(botao).toBeDisabled()
            expect(botao).toHaveAccessibleDescription(MOTIVO)
        }
    })

    it('BotaoTexto_FichaContatoEmpresaEmTeste_ConvidarAbreAJanela', async () => {
        app(PERMISSOES, EM_TESTE, { [`GET /api/v1/clientes/${C}`]: () => json(cliente) })
        await abrir(`/clientes/${C}?secao=contatos`)
        const convidar = await screen.findByRole('button', { name: 'Convidar Marta Souza para o portal' })
        expect(convidar).toBeEnabled()
        expect(convidar).not.toHaveAccessibleDescription()
        await userEvent.setup().click(convidar)
        expect(await screen.findByRole('dialog', { name: 'Convidar para o portal' })).toBeInTheDocument()
    })
})
