import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Tenant } from '@/shared/sessao/gravacao'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comSessao, daquiA, json, perfil, servidor } from '@/testes/app'

/**
 * Ctrl+Enter na caixa de mensagem (comentário da tarefa; a conversa da solicitação usa a mesma `CaixaMensagem`) segue
 * a regra do botão Enviar: com a empresa só leitura não envia (#191).
 */

const DIA = 86_400_000
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

function app(tenant: Partial<Tenant>) {
    return servidor({
        '/api/v1/auth/refresh': comSessao,
        '/api/v1/auth/eu': () =>
            json({ ...perfil, permissoes: ['tarefa.ver', 'tarefa.editar'], tenant: { ...perfil.tenant, ...tenant } }),
        '/api/v1/notificacoes': () => json({ itens: [], pagina: 1, tamanhoPagina: 10, total: 0, naoLidas: 0 }),
        'GET /api/v1/equipe': () => json([{ id: 'u1', nome: 'Ana Prado' }]),
        [`GET /api/v1/tarefas/${ID}`]: () => json(tarefa),
        [`GET /api/v1/tarefas/${ID}/comentarios`]: () => json([]),
        [`GET /api/v1/tarefas/${ID}/seguidores`]: () => json([]),
        [`POST /api/v1/tarefas/${ID}/comentarios`]: (corpo) =>
            json(
                {
                    id: 'c2',
                    autor: { id: perfil.usuarioId, nome: 'Ana Prado' },
                    texto: (corpo as { texto: string }).texto,
                    registradoEm: new Date().toISOString(),
                    anexos: [],
                },
                201,
            ),
    })
}

async function caixaDeComentario() {
    const aside = await screen.findByRole('complementary', { name: 'Trocar resistência' })
    const secao = await within(aside).findByRole('region', { name: 'Comentários' })
    return within(secao).getByRole('combobox', { name: 'Comentário' })
}

const comentario = (chamadas: ReturnType<typeof app>) =>
    chamadas.find((c) => c.metodo === 'POST' && c.caminho.endsWith('/comentarios'))

describe('Ctrl+Enter na caixa de mensagem (#191)', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('CaixaMensagem_CtrlEnterEmpresaVencida_NaoChamaAApi', async () => {
        const chamadas = app(VENCIDA)
        await abrir(`/tarefas/${ID}`)
        const caixa = await caixaDeComentario()
        const usuario = userEvent.setup()
        await usuario.type(caixa, 'Peça chegou')
        await usuario.keyboard('{Control>}{Enter}{/Control}')
        // Dá tempo de uma chamada sair, se fosse sair.
        await new Promise((r) => setTimeout(r, 50))
        expect(comentario(chamadas)).toBeUndefined()
        expect(caixa).toHaveValue('Peça chegou')
    })

    it('CaixaMensagem_CtrlEnterEmpresaEmTeste_EnviaOComentario', async () => {
        const chamadas = app(EM_TESTE)
        await abrir(`/tarefas/${ID}`)
        const caixa = await caixaDeComentario()
        const usuario = userEvent.setup()
        await usuario.type(caixa, 'Peça chegou')
        await usuario.keyboard('{Control>}{Enter}{/Control}')
        await vi.waitFor(() => expect(comentario(chamadas)?.corpo).toEqual({ texto: 'Peça chegou' }))
    })
})
