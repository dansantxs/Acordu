import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { hojeNoFuso, mesAtual, somarMeses } from '@/shared/format/datas'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, json, type Rotas } from '@/testes/app'

const PADARIA = '0199f0a0-0000-7000-8000-0000000000c1'
const ID = '0199f0a0-0000-7000-8000-0000000000e1'
const hoje = hojeNoFuso('America/Sao_Paulo')
const mes = mesAtual('America/Sao_Paulo')
const anterior = somarMeses(mes, -1)
const pagina = (itens: unknown[]) => json({ itens, pagina: 1, tamanhoPagina: 50, total: itens.length })

const sla = [
    { prioridade: 'URGENTE', primeiraRespostaMinutos: 60, resolucaoMinutos: 240 },
    { prioridade: 'ALTA', primeiraRespostaMinutos: 120, resolucaoMinutos: 960 },
    { prioridade: 'MEDIA', primeiraRespostaMinutos: 240, resolucaoMinutos: 1920 },
    { prioridade: 'BAIXA', primeiraRespostaMinutos: 480, resolucaoMinutos: 4800 },
]
const contrato = (mudancas: Record<string, unknown> = {}) => ({
    id: ID,
    clienteId: PADARIA,
    clienteNome: 'Padaria Bom Grão',
    tipo: 'BANCO_HORAS',
    inicio: `${somarMeses(mes, -6)}-01`,
    fim: null,
    franquiaMinutos: 1800,
    status: 'ATIVO',
    sla,
    periodosFechados: [],
    versao: '5',
    consumoAtual: null,
    ...mudancas,
})
const consumo = (periodo: string, mudancas: Record<string, unknown> = {}) => ({
    contratoId: ID,
    tipo: 'BANCO_HORAS',
    periodo,
    franquiaMinutos: 1800,
    consumidoMinutos: 180,
    naoFaturavelMinutos: 30,
    ajustesMinutos: 0,
    consumidoAcumuladoMinutos: 720,
    saldoMinutos: 1080,
    percentual: 40,
    periodoFechado: false,
    ...mudancas,
})

const app = (permissoes: string[], rotas: Rotas = {}) =>
    comPerfil(permissoes, {
        'GET /api/v1/contratos': () =>
            pagina([
                {
                    id: ID,
                    clienteId: PADARIA,
                    clienteNome: 'Padaria Bom Grão',
                    tipo: 'BANCO_HORAS',
                    inicio: '2026-01-01',
                    fim: null,
                    franquiaMinutos: 1800,
                    status: 'ATIVO',
                },
            ]),
        [`GET /api/v1/contratos/${ID}`]: () => json(contrato()),
        [`GET /api/v1/contratos/${ID}/consumo`]: (_, url) => json(consumo(url.searchParams.get('periodo')!)),
        'GET /api/v1/apontamentos': () => json({ itens: [], pagina: 1, tamanhoPagina: 200, total: 0 }),
        ...rotas,
    })

const GESTAO = [
    'contrato.ver',
    'contrato.gerenciar',
    'contrato.ajustar_saldo',
    'contrato.fechar_periodo',
    'cliente.ver',
]

describe('contratos', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('lista os ativos e cria um banco de horas com o SLA sugerido', async () => {
        const chamadas = app(GESTAO, {
            'GET /api/v1/clientes': (_, url) =>
                pagina(url.searchParams.get('busca') === 'pada' ? [{ id: PADARIA, nome: 'Padaria Bom Grão' }] : []),
            [`GET /api/v1/clientes/${PADARIA}`]: () => json({ id: PADARIA, nome: 'Padaria Bom Grão', contatos: [] }),
            'POST /api/v1/contratos': () => json(contrato(), 201),
        })
        const roteador = await abrir('/contratos')
        expect(await screen.findByRole('link', { name: 'Padaria Bom Grão' })).toBeInTheDocument()
        expect(chamadas.find((c) => c.caminho === '/api/v1/contratos')?.consulta.get('status')).toBe('ATIVO')
        const usuario = userEvent.setup()

        await usuario.click(screen.getByRole('button', { name: 'Novo contrato' }))
        const janela = await screen.findByRole('dialog', { name: 'Novo contrato' })
        await usuario.type(within(janela).getByRole('combobox', { name: 'Cliente' }), 'pada')
        await usuario.click(await screen.findByRole('option', { name: 'Padaria Bom Grão' }))
        await usuario.click(await within(janela).findByRole('combobox', { name: 'Tipo' }))
        await usuario.click(await screen.findByRole('option', { name: 'Banco de horas' }))
        // Sem as horas contratadas, o banco de horas não sai.
        await usuario.click(within(janela).getByRole('button', { name: 'Criar contrato' }))
        expect(within(janela).getByText('Informe as horas, ex.: 20h.')).toBeInTheDocument()
        expect(within(janela).getByLabelText('1ª resposta Urgente')).toHaveValue('1h')
        await usuario.type(within(janela).getByLabelText(/Horas contratadas/), '30h')
        const solucao = within(janela).getByLabelText('Solução Urgente')
        await usuario.clear(solucao)
        await usuario.type(solucao, '6h')
        await usuario.click(within(janela).getByRole('button', { name: 'Criar contrato' }))

        await vi.waitFor(() => expect(roteador.state.location.pathname).toBe(`/contratos/${ID}`))
        expect(chamadas.filter((c) => c.metodo === 'POST' && c.caminho === '/api/v1/contratos')).toHaveLength(1)
        expect(chamadas.find((c) => c.metodo === 'POST' && c.caminho === '/api/v1/contratos')?.corpo).toEqual({
            clienteId: PADARIA,
            tipo: 'BANCO_HORAS',
            inicio: hoje,
            fim: null,
            franquiaMinutos: 1800,
            sla: [{ ...sla[0], resolucaoMinutos: 360 }, sla[1], sla[2], sla[3]],
        })
    })

    it('novo contrato já vem com o SLA padrão da empresa', async () => {
        app(GESTAO, {
            'GET /api/v1/configuracoes/empresa': () =>
                json({
                    nome: 'Estúdio',
                    responderPara: null,
                    slaPadrao: [{ ...sla[0], primeiraRespostaMinutos: 30 }, sla[1], sla[2], sla[3]],
                    slaPersonalizado: true,
                }),
        })
        await abrir('/contratos?novo=1')
        const janela = await screen.findByRole('dialog', { name: 'Novo contrato' })
        expect(await within(janela).findByLabelText('1ª resposta Urgente')).toHaveValue('30min')
    })

    it('página mostra o saldo do banco de horas, fecha o mês anterior e ajusta o saldo', async () => {
        const chamadas = app(GESTAO, {
            [`POST /api/v1/contratos/${ID}/periodos/${anterior}/fechamento`]: () => new Response(null, { status: 204 }),
            [`POST /api/v1/contratos/${ID}/ajustes`]: () => new Response(null, { status: 201 }),
        })
        const roteador = await abrir(`/contratos/${ID}`)
        const usuario = userEvent.setup()

        expect(await screen.findByRole('heading', { name: 'Banco de horas · Padaria Bom Grão' })).toBeInTheDocument()
        expect(await screen.findByText('12h usadas de 30h')).toBeInTheDocument()
        expect(screen.getByText('18h')).toBeInTheDocument()
        // O mês corrente ainda não pode ser fechado.
        expect(screen.queryByRole('button', { name: 'Fechar o mês' })).not.toBeInTheDocument()

        await usuario.click(screen.getByRole('button', { name: 'Mês anterior' }))
        await vi.waitFor(() => expect(roteador.state.location.search).toEqual({ mes: anterior }))
        await usuario.click(await screen.findByRole('button', { name: 'Fechar o mês' }))
        const confirmacao = await screen.findByRole('dialog', { name: /^Fechar / })
        await usuario.click(within(confirmacao).getByRole('button', { name: 'Fechar o mês' }))
        await vi.waitFor(() =>
            expect(chamadas.some((c) => c.caminho.endsWith(`/periodos/${anterior}/fechamento`))).toBe(true),
        )

        await usuario.click(screen.getByRole('button', { name: 'Ajustar saldo' }))
        const janela = await screen.findByRole('dialog', { name: 'Ajustar saldo' })
        await usuario.click(within(janela).getByRole('radio', { name: 'Descontar horas' }))
        await usuario.type(within(janela).getByLabelText(/^Horas/), '2h')
        await usuario.type(within(janela).getByLabelText(/Motivo/), 'Correção de lançamento')
        await usuario.click(within(janela).getByRole('button', { name: 'Descontar' }))
        expect(await screen.findByText('Descontadas 2h do saldo.')).toBeInTheDocument()
        expect(chamadas.find((c) => c.caminho.endsWith('/ajustes'))?.corpo).toEqual({
            quantidadeMinutos: -120,
            motivo: 'Correção de lançamento',
            data: hoje,
        })
    })

    it('suspende direto e encerra com confirmação e data', async () => {
        let atual = contrato()
        const chamadas = app(GESTAO, {
            [`GET /api/v1/contratos/${ID}`]: () => json(atual),
            [`POST /api/v1/contratos/${ID}/suspensao`]: () => {
                atual = contrato({ status: 'SUSPENSO', versao: '6' })
                return json(atual)
            },
            [`POST /api/v1/contratos/${ID}/encerramento`]: () => {
                atual = contrato({ status: 'ENCERRADO', versao: '7' })
                return json(atual)
            },
        })
        await abrir(`/contratos/${ID}`)
        const usuario = userEvent.setup()

        await usuario.click(await screen.findByRole('button', { name: 'Suspender' }))
        expect(await screen.findByText('Contrato suspenso.')).toBeInTheDocument()
        expect(chamadas.find((c) => c.caminho.endsWith('/suspensao'))?.ifMatch).toBe('"5"')
        expect(await screen.findByRole('button', { name: 'Reativar' })).toBeInTheDocument()

        await usuario.click(screen.getByRole('button', { name: 'Encerrar' }))
        const confirmacao = await screen.findByRole('dialog', { name: 'Encerrar o contrato?' })
        expect(within(confirmacao).getByLabelText('Encerrar em')).toHaveValue(hoje)
        await usuario.click(within(confirmacao).getByRole('button', { name: 'Encerrar contrato' }))
        expect(await screen.findByText('Contrato encerrado.')).toBeInTheDocument()
        expect(chamadas.find((c) => c.caminho.endsWith('/encerramento'))).toMatchObject({
            ifMatch: '"6"',
            corpo: { data: hoje },
        })
        await vi.waitFor(() => expect(screen.queryByRole('button', { name: 'Editar' })).not.toBeInTheDocument())
    })
})
