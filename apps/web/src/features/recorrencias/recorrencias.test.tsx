import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, json, type Rotas } from '@/testes/app'

const C = '0199f0a0-0000-7000-8000-0000000000c1'
const R = '0199f0a0-0000-7000-8000-0000000000f1'
const pagina = (itens: unknown[]) => json({ itens, pagina: 1, tamanhoPagina: 50, total: itens.length })
const recorrencia = (mudancas: Record<string, unknown> = {}) => ({
    id: R,
    clienteId: C,
    clienteNome: 'Contábil Silva',
    contratoId: null,
    modelo: {
        titulo: 'Fechamento mensal',
        descricao: null,
        responsavelId: 'u1',
        estimativaMinutos: 240,
        visivelCliente: true,
        checklist: ['Conciliar extratos', 'Emitir guias'],
    },
    regra: { periodicidade: 'MENSAL', diaReferencia: 31, antecedenciaDias: 3, inicio: '2026-10-01', fim: null },
    proximaData: '2026-10-31',
    status: 'ATIVA',
    versao: '4',
    ...mudancas,
})

const app = (permissoes: string[], rotas: Rotas = {}) =>
    comPerfil(permissoes, {
        'GET /api/v1/equipe': () => json([{ id: 'u1', nome: 'Ana Prado' }]),
        'GET /api/v1/recorrencias': () =>
            pagina([
                {
                    id: R,
                    clienteId: C,
                    clienteNome: 'Contábil Silva',
                    contratoId: null,
                    titulo: 'Fechamento mensal',
                    responsavelId: 'u1',
                    periodicidade: 'MENSAL',
                    proximaData: '2026-10-31',
                    status: 'ATIVA',
                },
            ]),
        [`GET /api/v1/recorrencias/${R}`]: () => json(recorrencia()),
        [`GET /api/v1/recorrencias/${R}/previa`]: () => json(['2026-10-31', '2026-11-30', '2026-12-31']),
        ...rotas,
    })

const GESTAO = ['recorrencia.ver', 'recorrencia.gerenciar', 'cliente.ver']

describe('recorrências', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('lista as ativas e cria com a prévia das datas calculada pela API', async () => {
        const chamadas = app(GESTAO, {
            'GET /api/v1/clientes': (_, url) =>
                pagina(url.searchParams.get('busca') === 'cont' ? [{ id: C, nome: 'Contábil Silva' }] : []),
            [`GET /api/v1/clientes/${C}`]: () => json({ id: C, nome: 'Contábil Silva', contatos: [] }),
            'GET /api/v1/contratos': () => pagina([]),
            'POST /api/v1/recorrencias/previa': (corpo) =>
                (corpo as { regra: { diaReferencia: number } }).regra.diaReferencia === 31
                    ? json(['2026-10-31', '2026-11-30', '2027-02-28'])
                    : json(['2026-11-05']),
            'POST /api/v1/recorrencias': () => json(recorrencia(), 201),
        })
        const roteador = await abrir('/recorrencias')
        expect(await screen.findByRole('link', { name: 'Fechamento mensal' })).toBeInTheDocument()
        expect(screen.getByRole('cell', { name: 'Ana Prado' })).toBeInTheDocument()
        expect(chamadas.find((c) => c.caminho === '/api/v1/recorrencias')?.consulta.get('status')).toBe('ATIVA')
        const usuario = userEvent.setup()

        await usuario.click(screen.getByRole('button', { name: 'Nova recorrência' }))
        const janela = await screen.findByRole('dialog', { name: 'Nova recorrência' })
        await usuario.type(within(janela).getByRole('combobox', { name: 'Cliente' }), 'cont')
        await usuario.click(await screen.findByRole('option', { name: 'Contábil Silva' }))
        await usuario.type(await within(janela).findByRole('textbox', { name: /Título/ }), 'Fechamento mensal')
        await usuario.type(
            within(janela).getByRole('textbox', { name: /Checklist/ }),
            'Conciliar extratos{Enter}Emitir guias',
        )
        const dia = within(janela).getByLabelText('Dia do mês')
        await usuario.clear(dia)
        await usuario.type(dia, '31')
        expect(
            await within(janela).findByText(
                'Todo mês, no dia 31 (ou no último dia). A tarefa aparece 3 dias antes, com prazo na data.',
            ),
        ).toBeInTheDocument()
        expect(await within(janela).findByText(/28\/02\/27/)).toBeInTheDocument()

        await usuario.click(within(janela).getByRole('button', { name: 'Criar recorrência' }))
        await vi.waitFor(() => expect(roteador.state.location.pathname).toBe(`/recorrencias/${R}`))
        expect(chamadas.find((c) => c.metodo === 'POST' && c.caminho === '/api/v1/recorrencias')?.corpo).toEqual({
            clienteId: C,
            contratoId: null,
            modelo: {
                titulo: 'Fechamento mensal',
                descricao: null,
                responsavelId: null,
                estimativaMinutos: null,
                visivelCliente: false,
                checklist: ['Conciliar extratos', 'Emitir guias'],
            },
            regra: { periodicidade: 'MENSAL', diaReferencia: 31, antecedenciaDias: 3, inicio: null, fim: null },
        })
    })

    it('semanal escolhe o dia da semana e mostra o erro da regra que a API devolve', async () => {
        app(GESTAO, {
            'POST /api/v1/recorrencias/previa': (corpo) =>
                (corpo as { regra: { fim: string | null } }).regra.fim
                    ? json(
                          {
                              status: 400,
                              title: 'Dados inválidos',
                              errors: { 'regra.fim': ['O fim não pode ser antes do início.'] },
                          },
                          400,
                      )
                    : json(['2026-10-26', '2026-11-02']),
        })
        await abrir('/recorrencias?nova=1')
        const janela = await screen.findByRole('dialog', { name: 'Nova recorrência' })
        const usuario = userEvent.setup()
        await usuario.click(within(janela).getByRole('radio', { name: 'Toda semana' }))
        expect(within(janela).getByRole('combobox', { name: /Dia da semana/ })).toHaveValue('segunda-feira')
        expect(await within(janela).findByText(/26\/10/)).toBeInTheDocument()

        await usuario.type(within(janela).getByLabelText('Termina em'), '2020-01-01')
        expect(await within(janela).findByText('O fim não pode ser antes do início.')).toBeInTheDocument()
    })

    it('painel mostra a regra e as próximas datas, pausa e encerra com confirmação', async () => {
        let atual = recorrencia()
        const chamadas = app(GESTAO, {
            [`GET /api/v1/recorrencias/${R}`]: () => json(atual),
            [`POST /api/v1/recorrencias/${R}/pausa`]: () => {
                atual = recorrencia({ status: 'PAUSADA', versao: '5' })
                return json(atual)
            },
            [`DELETE /api/v1/recorrencias/${R}`]: () => {
                atual = recorrencia({ status: 'ENCERRADA', versao: '6', proximaData: null })
                return new Response(null, { status: 204 })
            },
        })
        await abrir(`/recorrencias/${R}`)
        const painel = await screen.findByRole('complementary', { name: 'Fechamento mensal' })
        const usuario = userEvent.setup()
        expect(within(painel).getByText('Todo mês, no dia 31 (ou no último dia)')).toBeInTheDocument()
        expect(await within(painel).findByText(/30\/11/)).toBeInTheDocument()
        expect(within(painel).getByText('Conciliar extratos')).toBeInTheDocument()
        expect(within(painel).getByText('4h')).toBeInTheDocument()

        await usuario.click(within(painel).getByRole('button', { name: 'Pausar' }))
        expect(await screen.findByText('Recorrência pausada: nenhuma tarefa nova até retomar.')).toBeInTheDocument()
        expect(chamadas.find((c) => c.caminho.endsWith('/pausa'))?.ifMatch).toBe('"4"')
        expect(await within(painel).findByText('Datas se retomar hoje')).toBeInTheDocument()

        await usuario.click(within(painel).getByRole('button', { name: 'Encerrar' }))
        const confirmacao = await screen.findByRole('dialog', { name: 'Encerrar a recorrência?' })
        await usuario.click(within(confirmacao).getByRole('button', { name: 'Encerrar recorrência' }))
        expect(await screen.findByText('Recorrência encerrada.')).toBeInTheDocument()
        expect(chamadas.find((c) => c.metodo === 'DELETE')?.ifMatch).toBe('"5"')
        expect(await within(painel).findByText('Encerrada: não gera mais tarefas.')).toBeInTheDocument()
        expect(within(painel).queryByRole('button', { name: 'Editar' })).not.toBeInTheDocument()
    })

    it('quem só vê não cria nem pausa', async () => {
        app(['recorrencia.ver'])
        await abrir(`/recorrencias/${R}`)
        const painel = await screen.findByRole('complementary', { name: 'Fechamento mensal' })
        expect(screen.queryByRole('button', { name: 'Nova recorrência' })).not.toBeInTheDocument()
        expect(within(painel).queryByRole('button', { name: 'Pausar' })).not.toBeInTheDocument()
    })
})
