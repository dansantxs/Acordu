import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, daquiA, json, type Rotas } from '@/testes/app'

const ID = '0199f0a0-0000-7000-8000-0000000000b1'
const semCronometro = () => new Response(null, { status: 204 })
const cronometro = (tarefa = { id: ID, nome: 'Trocar resistência' }, minutos = 65) => ({
    id: 'k1',
    tarefa,
    iniciadoEm: daquiA(-minutos * 60_000),
    decorridoMinutos: minutos,
})

const tarefa = {
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
}

const app = (rotas: Rotas) =>
    comPerfil(['tarefa.ver', 'apontamento.criar'], {
        'GET /api/v1/tarefas': () => json({ itens: [], pagina: 1, tamanhoPagina: 50, total: 0 }),
        [`GET /api/v1/tarefas/${ID}`]: () => json(tarefa),
        [`GET /api/v1/tarefas/${ID}/comentarios`]: () => json([]),
        [`GET /api/v1/tarefas/${ID}/seguidores`]: () => json([]),
        'GET /api/v1/equipe': () => json([]),
        ...rotas,
    })

const indicador = () => screen.findByRole('status', { name: 'Cronômetro ativo' })

describe('cronômetro', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('inicia na tarefa e aparece na capa lateral', async () => {
        let ativo: ReturnType<typeof cronometro> | null = null
        const chamadas = app({
            'GET /api/v1/cronometro': () => (ativo ? json(ativo) : semCronometro()),
            'POST /api/v1/cronometro/inicio': () => {
                ativo = cronometro(undefined, 0)
                return json(ativo, 201)
            },
        })
        await abrir(`/tarefas/${ID}`)
        const usuario = userEvent.setup()
        const aside = await screen.findByRole('complementary', { name: 'Trocar resistência' })
        expect(screen.queryByRole('status', { name: 'Cronômetro ativo' })).not.toBeInTheDocument()

        await usuario.click(await within(aside).findByRole('button', { name: 'Iniciar cronômetro' }))
        expect(chamadas.find((c) => c.caminho === '/api/v1/cronometro/inicio')?.corpo).toEqual({ tarefaId: ID })
        expect(within(await indicador()).getByRole('link', { name: 'Trocar resistência' })).toBeInTheDocument()
        expect(await within(aside).findByRole('button', { name: /Parar cronômetro · 0min/ })).toBeInTheDocument()
    })

    it('parar abre o formulário preenchido; sem faturável, desmarca o visível', async () => {
        let ativo: ReturnType<typeof cronometro> | null = cronometro()
        const chamadas = app({
            'GET /api/v1/cronometro': () => (ativo ? json(ativo) : semCronometro()),
            'POST /api/v1/cronometro/parada': () => {
                ativo = null
                return json({ id: 'a1', duracaoMinutos: 65, tarefa: { id: ID, nome: 'Trocar resistência' } }, 201)
            },
        })
        await abrir('/tarefas')
        const usuario = userEvent.setup()

        await usuario.click(within(await indicador()).getByRole('button', { name: 'Parar' }))
        const janela = await screen.findByRole('dialog', { name: 'Parar cronômetro' })
        expect(within(janela).getByText('1h05')).toBeInTheDocument()
        expect(within(janela).getByLabelText('O que foi feito')).toHaveValue('Trocar resistência')
        await usuario.type(await within(janela).findByLabelText('Descrição para o cliente'), 'Peça trocada')
        expect(within(janela).getByRole('checkbox', { name: 'Visível ao cliente' })).toBeChecked()
        await usuario.click(within(janela).getByRole('checkbox', { name: /Faturável/ }))
        expect(within(janela).getByRole('checkbox', { name: 'Visível ao cliente' })).not.toBeChecked()
        await usuario.click(within(janela).getByRole('button', { name: 'Parar e apontar' }))

        expect(await screen.findByText('1h05 apontadas em "Trocar resistência".')).toBeInTheDocument()
        expect(chamadas.find((c) => c.caminho === '/api/v1/cronometro/parada')?.corpo).toEqual({
            descricao: 'Trocar resistência',
            descricaoExterna: 'Peça trocada',
            faturavel: false,
            visivelCliente: false,
        })
        await vi.waitFor(() =>
            expect(screen.queryByRole('status', { name: 'Cronômetro ativo' })).not.toBeInTheDocument(),
        )
    })

    it('em outra tarefa, avisa em vez de oferecer iniciar; descartar pede confirmação', async () => {
        let ativo: ReturnType<typeof cronometro> | null = cronometro({ id: 'outra', nome: 'Arte do banner' }, 5)
        const chamadas = app({
            'GET /api/v1/cronometro': () => (ativo ? json(ativo) : semCronometro()),
            '/api/v1/tarefas/outra': () => json({ ...tarefa, id: 'outra', titulo: 'Arte do banner' }),
            'DELETE /api/v1/cronometro': () => {
                ativo = null
                return new Response(null, { status: 204 })
            },
        })
        await abrir(`/tarefas/${ID}`)
        const usuario = userEvent.setup()
        const aside = await screen.findByRole('complementary', { name: 'Trocar resistência' })
        expect(await within(aside).findByText(/Cronômetro correndo em "Arte do banner"/)).toBeInTheDocument()
        expect(within(aside).queryByRole('button', { name: 'Iniciar cronômetro' })).not.toBeInTheDocument()

        await usuario.click(within(await indicador()).getByRole('button', { name: 'Parar' }))
        const janela = await screen.findByRole('dialog', { name: 'Parar cronômetro' })
        await usuario.click(within(janela).getByRole('button', { name: 'Descartar sem apontar' }))
        const confirmacao = await screen.findByRole('dialog', { name: 'Descartar o cronômetro?' })
        await usuario.click(within(confirmacao).getByRole('button', { name: 'Descartar' }))

        expect(await screen.findByText('Cronômetro descartado, sem apontar.')).toBeInTheDocument()
        expect(chamadas.some((c) => c.metodo === 'DELETE' && c.caminho === '/api/v1/cronometro')).toBe(true)
        expect(await within(aside).findByRole('button', { name: 'Iniciar cronômetro' })).toBeInTheDocument()
    })
})
