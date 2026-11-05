import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { hojeNoFuso, segundaDaSemana, somarDias } from '@/shared/format/datas'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, json, perfil, type Rotas } from '@/testes/app'

const RAFAEL = '0199f0a0-0000-7000-8000-000000000002'
const hoje = hojeNoFuso('America/Sao_Paulo')
const segunda = segundaDaSemana(hoje)

const apontamento = (mudancas: Record<string, unknown>) => ({
    id: crypto.randomUUID(),
    versao: '1',
    tarefa: { id: 't1', nome: 'Trocar resistência' },
    clienteId: 'c1',
    usuario: { id: perfil.usuarioId, nome: 'Ana Prado' },
    contratoId: null,
    data: segunda,
    duracaoMinutos: 60,
    descricao: 'Troca da peça',
    descricaoExterna: null,
    faturavel: true,
    visivelCliente: true,
    ...mudancas,
})
const pagina = (itens: unknown[]) => json({ itens, pagina: 1, tamanhoPagina: 200, total: itens.length })

const app = (permissoes: string[], rotas: Rotas = {}) =>
    comPerfil(permissoes, {
        'GET /api/v1/apontamentos': () => pagina([]),
        'GET /api/v1/equipe': () =>
            json([
                { id: perfil.usuarioId, nome: 'Ana Prado' },
                { id: RAFAEL, nome: 'Rafael Moura' },
            ]),
        ...rotas,
    })

const APONTAR = ['apontamento.ver', 'apontamento.criar']
const ultima = (chamadas: { caminho: string; metodo: string; consulta: URLSearchParams }[]) =>
    chamadas.filter((c) => c.caminho === '/api/v1/apontamentos' && c.metodo === 'GET').at(-1)?.consulta

describe('horas', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('mostra a minha semana por dia, com os totais, e navega entre semanas', async () => {
        const chamadas = app(APONTAR, {
            'GET /api/v1/apontamentos': () =>
                pagina([
                    apontamento({
                        data: somarDias(segunda, 1),
                        duracaoMinutos: 30,
                        faturavel: false,
                        descricao: 'Reunião',
                    }),
                    apontamento({ data: segunda, duracaoMinutos: 90 }),
                    apontamento({ data: segunda, duracaoMinutos: 15, descricao: 'Teste final' }),
                ]),
        })
        const roteador = await abrir('/horas')

        expect(
            await screen.findByRole('heading', { level: 1, name: 'Horas trabalhadas na semana' }),
        ).toBeInTheDocument()
        expect(await screen.findByText('2h15')).toBeInTheDocument()
        expect(screen.getByText(/1h45 faturáveis/)).toBeInTheDocument()
        const diaSegunda = within(screen.getByRole('region', { name: /segunda-feira/ }))
        expect(diaSegunda.getByText('1h45')).toBeInTheDocument()
        expect(diaSegunda.getByText('Teste final')).toBeInTheDocument()
        expect(
            within(screen.getByRole('region', { name: /terça-feira/ })).getByText('não faturável'),
        ).toBeInTheDocument()
        expect(ultima(chamadas)?.get('usuarioId')).toBe(perfil.usuarioId)
        expect(ultima(chamadas)?.get('de')).toBe(segunda)
        expect(ultima(chamadas)?.get('ate')).toBe(somarDias(segunda, 6))

        await userEvent.setup().click(screen.getByRole('link', { name: 'Semana anterior' }))
        await vi.waitFor(() => expect(roteador.state.location.search).toEqual({ semana: somarDias(segunda, -7) }))
        await vi.waitFor(() => expect(ultima(chamadas)?.get('de')).toBe(somarDias(segunda, -7)))
    })

    it('"toda a equipe" tira o filtro de pessoa e só edita as próprias sem editar_todos', async () => {
        const chamadas = app(APONTAR, {
            'GET /api/v1/apontamentos': () =>
                pagina([
                    apontamento({ descricao: 'Minha' }),
                    apontamento({ descricao: 'Do Rafael', usuario: { id: RAFAEL, nome: 'Rafael Moura' } }),
                ]),
        })
        await abrir('/horas?pessoa=equipe')
        expect(await screen.findByText('Do Rafael')).toBeInTheDocument()
        expect(ultima(chamadas)?.has('usuarioId')).toBe(false)
        expect(screen.getByText('Rafael Moura')).toBeInTheDocument()
        expect(screen.getByRole('button', { name: 'Editar 1h de Minha' })).toBeInTheDocument()
        expect(screen.queryByRole('button', { name: 'Editar 1h de Do Rafael' })).not.toBeInTheDocument()
    })

    it('lança horas escolhendo a tarefa, edita com If-Match e exclui com confirmação', async () => {
        const minha = apontamento({ descricao: 'Troca da peça', versao: '4' })
        const chamadas = app(APONTAR, {
            'GET /api/v1/apontamentos': () => pagina([minha]),
            'GET /api/v1/tarefas': (_, url) =>
                pagina(
                    url.searchParams.get('busca') === 'troc'
                        ? [
                              {
                                  id: 't1',
                                  titulo: 'Trocar resistência',
                                  status: 'FAZENDO',
                                  clienteId: 'c1',
                                  clienteNome: 'Padaria Bom Grão',
                                  solicitacaoNumero: 1042,
                                  projetoNome: null,
                              },
                          ]
                        : [],
                ),
            'POST /api/v1/apontamentos': () => json(apontamento({ duracaoMinutos: 90 }), 201),
            [`PATCH /api/v1/apontamentos/${minha.id}`]: () => json(minha),
            [`DELETE /api/v1/apontamentos/${minha.id}`]: () => new Response(null, { status: 204 }),
        })
        await abrir('/horas')
        const usuario = userEvent.setup()
        await screen.findByText('Troca da peça')

        await usuario.click(screen.getByRole('button', { name: 'Lançar horas' }))
        const janela = await screen.findByRole('dialog', { name: 'Lançar horas' })
        await usuario.type(within(janela).getByRole('combobox', { name: 'Tarefa' }), 'troc')
        await usuario.click(await screen.findByRole('option', { name: /Trocar resistência/ }))
        await usuario.type(within(janela).getByLabelText(/Duração/), '1h30')
        await usuario.type(within(janela).getByLabelText('O que foi feito'), 'Instalação')
        await usuario.type(within(janela).getByLabelText('Descrição para o cliente'), 'Peça instalada')
        await usuario.click(within(janela).getByRole('button', { name: 'Lançar' }))

        expect(await screen.findByText('1h30 apontadas em "Trocar resistência".')).toBeInTheDocument()
        expect(chamadas.find((c) => c.metodo === 'POST' && c.caminho === '/api/v1/apontamentos')?.corpo).toEqual({
            tarefaId: 't1',
            data: hoje,
            duracaoMinutos: 90,
            descricao: 'Instalação',
            descricaoExterna: 'Peça instalada',
            faturavel: true,
            visivelCliente: true,
        })

        await usuario.click(screen.getByRole('button', { name: 'Editar 1h de Troca da peça' }))
        const edicao = await screen.findByRole('dialog', { name: 'Editar horas' })
        const duracao = within(edicao).getByLabelText(/Duração/)
        expect(duracao).toHaveValue('1h')
        await usuario.clear(duracao)
        await usuario.type(duracao, '45')
        await usuario.click(within(edicao).getByRole('button', { name: 'Salvar' }))
        await vi.waitFor(() =>
            expect(chamadas.find((c) => c.metodo === 'PATCH')).toMatchObject({
                ifMatch: '"4"',
                corpo: { duracaoMinutos: 45, descricao: 'Troca da peça', data: segunda },
            }),
        )

        await usuario.click(await screen.findByRole('button', { name: 'Excluir 1h de Troca da peça' }))
        const confirmacao = await screen.findByRole('dialog', { name: 'Excluir este apontamento?' })
        await usuario.click(within(confirmacao).getByRole('button', { name: 'Excluir apontamento' }))
        expect(await screen.findByText('Apontamento excluído.')).toBeInTheDocument()
        expect(chamadas.some((c) => c.metodo === 'DELETE')).toBe(true)
    })

    it('no painel da tarefa, lista as horas e lança direto nela', async () => {
        const ID = '0199f0a0-0000-7000-8000-0000000000b1'
        const chamadas = app([...APONTAR, 'tarefa.ver'], {
            'GET /api/v1/apontamentos': () => pagina([apontamento({ tarefa: { id: ID, nome: 'Trocar resistência' } })]),
            'GET /api/v1/tarefas': () => pagina([]),
            [`GET /api/v1/tarefas/${ID}`]: () =>
                json({
                    id: ID,
                    versao: '1',
                    titulo: 'Trocar resistência',
                    descricao: null,
                    status: 'FAZENDO',
                    clienteId: null,
                    solicitacaoId: null,
                    projetoId: null,
                    recorrenciaId: null,
                    responsavel: null,
                    prazo: null,
                    estimativaMinutos: null,
                    visivelCliente: false,
                    planejadaPara: null,
                    apontadoMinutos: 60,
                    checklist: [],
                    concluidaEm: null,
                    clienteNome: null,
                    solicitacaoNumero: null,
                    projetoNome: null,
                }),
            [`GET /api/v1/tarefas/${ID}/comentarios`]: () => json([]),
            [`GET /api/v1/tarefas/${ID}/seguidores`]: () => json([]),
            'GET /api/v1/cronometro': () => new Response(null, { status: 204 }),
            'POST /api/v1/apontamentos': () => json(apontamento({ duracaoMinutos: 30 }), 201),
        })
        await abrir(`/tarefas/${ID}`)
        const aside = await screen.findByRole('complementary', { name: 'Trocar resistência' })
        const horas = within(await within(aside).findByRole('region', { name: /Horas/ }))
        expect(await horas.findByText('Troca da peça')).toBeInTheDocument()
        expect(chamadas.find((c) => c.caminho === '/api/v1/apontamentos')?.consulta.get('tarefaId')).toBe(ID)

        const usuario = userEvent.setup()
        await usuario.click(horas.getByRole('button', { name: 'Lançar horas' }))
        const janela = await screen.findByRole('dialog', { name: 'Lançar horas' })
        // Tarefa sem cliente: sem descrição para o cliente nem "visível".
        expect(within(janela).queryByLabelText('Descrição para o cliente')).not.toBeInTheDocument()
        await usuario.type(within(janela).getByLabelText(/Duração/), '30')
        await usuario.type(within(janela).getByLabelText('O que foi feito'), 'Revisão')
        await usuario.click(within(janela).getByRole('button', { name: 'Lançar' }))
        await vi.waitFor(() =>
            expect(
                chamadas.find((c) => c.metodo === 'POST' && c.caminho === '/api/v1/apontamentos')?.corpo,
            ).toMatchObject({
                tarefaId: ID,
                duracaoMinutos: 30,
                visivelCliente: false,
            }),
        )
    })
})
