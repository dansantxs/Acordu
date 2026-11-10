import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comSessao, json, perfil, servidor, type Rotas } from '@/testes/app'

const problema = (status: number, corpo: Record<string, unknown>) =>
    new Response(JSON.stringify({ status, ...corpo }), {
        status,
        headers: { 'content-type': 'application/problem+json' },
    })

function app(extras: Rotas = {}) {
    return servidor({
        '/api/v1/auth/refresh': comSessao,
        '/api/v1/auth/eu': () => json({ ...perfil, permissoes: [] }),
        '/api/v1/notificacoes': () => json({ itens: [], pagina: 1, tamanhoPagina: 10, total: 0, naoLidas: 0 }),
        ...extras,
    })
}

async function abrirJanela() {
    const usuario = userEvent.setup()
    await usuario.click(await screen.findByRole('button', { name: /Ana Prado/ }))
    await usuario.click(await screen.findByRole('menuitem', { name: 'Sugerir melhoria' }))
    return { usuario, janela: await screen.findByRole('dialog', { name: 'Sugerir melhoria' }) }
}

describe('Sugerir melhoria (F13f)', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('qualquer papel abre pela conta, com o aviso de não escrever dados de clientes (F13-22)', async () => {
        app()
        await abrir('/')
        const { janela } = await abrirJanela()
        expect(
            within(janela).getByText('Não escreva dados dos seus clientes: a sugestão vai para a equipe do Acordu.'),
        ).toBeInTheDocument()
        expect(within(janela).getByRole('button', { name: 'Enviar' })).toBeDisabled()
    })

    it('envia o texto e a tela de onde veio, e agradece (F13-23)', async () => {
        const chamadas = app({ 'POST /api/v1/sugestoes': () => json({ id: 'x', numero: 12 }, 201) })
        await abrir('/')
        const { usuario, janela } = await abrirJanela()
        await usuario.type(within(janela).getByRole('textbox'), '  Filtrar tarefas por cliente  ')
        await usuario.click(within(janela).getByRole('button', { name: 'Enviar' }))

        expect(await screen.findByText('Obrigado! A sugestão foi para a equipe do Acordu.')).toBeInTheDocument()
        expect(screen.queryByRole('dialog', { name: 'Sugerir melhoria' })).not.toBeInTheDocument()
        const envio = chamadas.find((c) => c.metodo === 'POST' && c.caminho === '/api/v1/sugestoes')
        expect(envio?.corpo).toEqual({ texto: 'Filtrar tarefas por cliente', tela: '/' })
    })

    it('a tela vai como rota modelo, sem o id (F13-23)', async () => {
        const chamadas = app({ 'POST /api/v1/sugestoes': () => json({ id: 'x', numero: 13 }, 201) })
        await abrir('/inexistente/7c1f0e2a-1b2c-4d5e-8f90-a1b2c3d4e5f6?aba=2')
        const { usuario, janela } = await abrirJanela()
        await usuario.type(within(janela).getByRole('textbox'), 'Uma tela que não existe')
        await usuario.click(within(janela).getByRole('button', { name: 'Enviar' }))

        await screen.findByText('Obrigado! A sugestão foi para a equipe do Acordu.')
        expect(chamadas.find((c) => c.caminho === '/api/v1/sugestoes')?.corpo).toMatchObject({
            tela: '/inexistente/:id',
        })
    })

    it('Cancelar depois de um erro: ao reabrir, a janela vem limpa', async () => {
        app({
            'POST /api/v1/sugestoes': () =>
                problema(429, { type: 'https://acordu.app.br/erros/limite-requisicoes', detail: 'Muitas sugestões.' }),
        })
        await abrir('/')
        const { usuario, janela } = await abrirJanela()
        await usuario.type(within(janela).getByRole('textbox'), 'Uma sugestão a mais')
        await usuario.click(within(janela).getByRole('button', { name: 'Enviar' }))
        await within(janela).findByRole('alert')
        await usuario.click(within(janela).getByRole('button', { name: 'Cancelar' }))

        const { janela: deNovo } = await abrirJanela()
        expect(within(deNovo).queryByRole('alert')).not.toBeInTheDocument()
    })

    it('o contador conta sem os espaços das pontas, como o envio', async () => {
        app()
        await abrir('/')
        const { usuario, janela } = await abrirJanela()
        await usuario.type(within(janela).getByRole('textbox'), '   nove     ')

        expect(within(janela).getByText(/\(4\/2\.000\)/)).toBeInTheDocument()
        expect(within(janela).getByRole('button', { name: 'Enviar' })).toBeDisabled()
    })

    it('o erro do campo aparece no campo (F13-24)', async () => {
        app({
            'POST /api/v1/sugestoes': () =>
                problema(400, {
                    type: 'https://acordu.app.br/erros/validacao',
                    title: 'Dados inválidos',
                    errors: { texto: ['Escreva pelo menos 10 caracteres.'] },
                }),
        })
        await abrir('/')
        const { usuario, janela } = await abrirJanela()
        await usuario.type(within(janela).getByRole('textbox'), 'Dez letras aqui')
        await usuario.click(within(janela).getByRole('button', { name: 'Enviar' }))

        expect(await within(janela).findByText('Escreva pelo menos 10 caracteres.')).toBeInTheDocument()
    })

    it('muitas sugestões na hora: a mensagem do limite (F13-30)', async () => {
        app({
            'POST /api/v1/sugestoes': () =>
                problema(429, {
                    type: 'https://acordu.app.br/erros/limite-requisicoes',
                    title: 'Muitas requisições',
                    detail: 'Muitas sugestões em pouco tempo. Tente de novo mais tarde.',
                }),
        })
        await abrir('/')
        const { usuario, janela } = await abrirJanela()
        await usuario.type(within(janela).getByRole('textbox'), 'Uma sugestão a mais')
        await usuario.click(within(janela).getByRole('button', { name: 'Enviar' }))

        expect(await within(janela).findByRole('alert')).toHaveTextContent(
            'Muitas sugestões em pouco tempo. Tente de novo mais tarde.',
        )
    })
})
