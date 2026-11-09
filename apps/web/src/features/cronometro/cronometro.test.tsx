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

const app = (rotas: Rotas, extras: string[] = []) =>
    comPerfil(['tarefa.ver', 'apontamento.criar', ...extras], {
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
        expect(chamadas.find((c) => c.caminho === '/api/v1/cronometro/inicio')?.corpo).toEqual({
            tarefaId: ID,
            iniciadoEm: null,
        })
        expect(within(await indicador()).getByRole('link', { name: 'Trocar resistência' })).toBeInTheDocument()
        expect(await within(aside).findByRole('button', { name: /Parar cronômetro · 0min/ })).toBeInTheDocument()
    })

    it('iniciar numa tarefa a fazer recarrega a tarefa, que passou a fazendo (F11-19)', async () => {
        let ativo: ReturnType<typeof cronometro> | null = null
        let atual = { ...tarefa, status: 'A_FAZER' }
        app(
            {
                [`GET /api/v1/tarefas/${ID}`]: () => json(atual),
                'GET /api/v1/cronometro': () => (ativo ? json(ativo) : semCronometro()),
                'POST /api/v1/cronometro/inicio': () => {
                    ativo = cronometro(undefined, 0)
                    atual = { ...tarefa, status: 'FAZENDO', versao: '4' }
                    return json(ativo, 201)
                },
            },
            ['tarefa.editar'],
        )
        await abrir(`/tarefas/${ID}`)
        const usuario = userEvent.setup()
        const aside = await screen.findByRole('complementary', { name: 'Trocar resistência' })
        expect(await within(aside).findByRole('button', { name: 'Iniciar' })).toBeInTheDocument()

        await usuario.click(await within(aside).findByRole('button', { name: 'Iniciar cronômetro' }))
        expect(await within(aside).findByRole('button', { name: 'Concluir' })).toBeInTheDocument()
        expect(within(aside).queryByRole('button', { name: 'Iniciar' })).not.toBeInTheDocument()
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
    it('começou antes: manda o horário informado (F12-01) e mostra o erro da API na janela (F12-03)', async () => {
        let tentativas = 0
        const chamadas = app({
            'GET /api/v1/cronometro': () => semCronometro(),
            'POST /api/v1/cronometro/inicio': () => {
                tentativas++
                return tentativas === 1
                    ? json({ status: 400, errors: { iniciadoEm: ['O início pode ser de até 12 horas atrás.'] } }, 400)
                    : json(cronometro(undefined, 20), 201)
            },
        })
        await abrir(`/tarefas/${ID}`)
        const usuario = userEvent.setup()
        const aside = await screen.findByRole('complementary', { name: 'Trocar resistência' })
        await usuario.click(await within(aside).findByRole('button', { name: 'Começou antes?' }))
        const janela = await screen.findByRole('dialog', { name: 'Começou antes' })
        await usuario.type(within(janela).getByLabelText('Começou às'), '08:40')
        await usuario.click(within(janela).getByRole('button', { name: 'Iniciar cronômetro' }))

        expect(await within(janela).findByText('O início pode ser de até 12 horas atrás.')).toBeInTheDocument()
        const enviado = chamadas.find((c) => c.caminho === '/api/v1/cronometro/inicio')?.corpo as { iniciadoEm: string }
        // 08:40 no fuso da empresa, qualquer que seja o fuso da máquina que roda o teste.
        const noFuso = new Intl.DateTimeFormat('pt-BR', {
            timeZone: 'America/Sao_Paulo',
            hour: '2-digit',
            minute: '2-digit',
        }).format(new Date(enviado.iniciadoEm))
        expect(noFuso).toBe('08:40')
        await usuario.click(within(janela).getByRole('button', { name: 'Iniciar cronômetro' }))
        await vi.waitFor(() => expect(screen.queryByRole('dialog', { name: 'Começou antes' })).not.toBeInTheDocument())
    })

    it('parar com outra duração manda a escolhida; inválida não manda (F12-04 a F12-06)', async () => {
        const chamadas = app({
            'GET /api/v1/cronometro': () => json(cronometro()),
            'POST /api/v1/cronometro/parada': () =>
                json({ id: 'a1', duracaoMinutos: 45, tarefa: { id: ID, nome: 'Trocar resistência' } }, 201),
        })
        await abrir('/tarefas')
        const usuario = userEvent.setup()
        await usuario.click(within(await indicador()).getByRole('button', { name: 'Parar' }))
        const janela = await screen.findByRole('dialog', { name: 'Parar cronômetro' })
        const duracao = within(janela).getByLabelText('Duração')
        expect(duracao).toHaveValue('1h05')

        await usuario.clear(duracao)
        await usuario.type(duracao, '25h')
        await usuario.click(within(janela).getByRole('button', { name: 'Parar e apontar' }))
        expect(await within(janela).findByText(/De 1 minuto a 24 horas/)).toBeInTheDocument()
        expect(chamadas.some((c) => c.caminho === '/api/v1/cronometro/parada')).toBe(false)

        await usuario.clear(duracao)
        await usuario.type(duracao, '45min')
        await usuario.click(within(janela).getByRole('button', { name: 'Parar e apontar' }))
        await vi.waitFor(() =>
            expect(chamadas.find((c) => c.caminho === '/api/v1/cronometro/parada')?.corpo).toMatchObject({
                duracaoMinutos: 45,
            }),
        )
    })
})
