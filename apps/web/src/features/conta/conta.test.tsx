import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reiniciarParaTestes } from '@/shared/sessao/sessao'
import { abrir, comPerfil, json, type Rotas } from '@/testes/app'

const preferencias = {
    categorias: [
        { categoria: 'NOVA_SOLICITACAO', email: true },
        { categoria: 'MENCAO', email: true },
        { categoria: 'SEGUIDAS', email: false },
    ],
}
const app = (permissoes: string[], rotas: Rotas = {}) =>
    comPerfil(permissoes, {
        'GET /api/v1/notificacoes/preferencias': () => json(preferencias),
        'GET /api/v1/agenda/assinatura': () => json({ ativa: false, discreta: false, criadaEm: null }),
        ...rotas,
    })

describe('minha conta', () => {
    beforeEach(() => reiniciarParaTestes())
    afterEach(() => vi.unstubAllGlobals())

    it('abre pelo menu do usuário e salva os e-mails escolhidos', async () => {
        const chamadas = app(['tarefa.ver'], {
            'PUT /api/v1/notificacoes/preferencias': (corpo) => json(corpo),
        })
        const roteador = await abrir('/')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('button', { name: /Ana Prado/ }))
        await usuario.click(await screen.findByRole('menuitem', { name: 'Minha conta' }))
        await vi.waitFor(() => expect(roteador.state.location.pathname).toBe('/conta'))

        await usuario.click(await screen.findByRole('checkbox', { name: 'O que você segue' }))
        await usuario.click(screen.getByRole('checkbox', { name: 'Menções' }))
        await usuario.click(screen.getByRole('button', { name: 'Salvar' }))
        expect(await screen.findByText('Preferências de e-mail salvas.')).toBeInTheDocument()
        expect(chamadas.find((c) => c.metodo === 'PUT')?.corpo).toEqual({
            categorias: [
                { categoria: 'NOVA_SOLICITACAO', email: true },
                { categoria: 'MENCAO', email: false },
                { categoria: 'SEGUIDAS', email: true },
            ],
        })
    })

    it('gera o link da agenda, mostra uma vez e troca para o modo discreto', async () => {
        let assinatura = { ativa: false, discreta: false, criadaEm: null as string | null }
        const chamadas = app(['tarefa.ver'], {
            'GET /api/v1/agenda/assinatura': () => json(assinatura),
            'POST /api/v1/agenda/assinatura': () => {
                assinatura = { ativa: true, discreta: false, criadaEm: '2026-10-22T15:00:00Z' }
                return json({ url: 'https://acordu.app.br/api/v1/agenda/tok.ics', discreta: false }, 201)
            },
            'PATCH /api/v1/agenda/assinatura': (corpo) => {
                assinatura = { ...assinatura, ...(corpo as object) }
                return json(assinatura)
            },
        })
        await abrir('/conta')
        const usuario = userEvent.setup()
        await usuario.click(await screen.findByRole('button', { name: 'Gerar link' }))
        expect(await screen.findByLabelText('Seu link')).toHaveValue('https://acordu.app.br/api/v1/agenda/tok.ics')
        expect(chamadas.find((c) => c.metodo === 'POST' && c.caminho.endsWith('/assinatura'))?.corpo).toEqual({
            discreta: false,
        })

        await usuario.click(screen.getByRole('checkbox', { name: /Modo discreto/ }))
        expect(await screen.findByText('Agenda em modo discreto.')).toBeInTheDocument()
        expect(chamadas.find((c) => c.metodo === 'PATCH')?.corpo).toEqual({ discreta: true })

        await usuario.click(screen.getByRole('button', { name: 'Gerar novo link' }))
        const confirmacao = await screen.findByRole('dialog', { name: 'Gerar um link novo?' })
        expect(within(confirmacao).getByText(/O link atual para de funcionar/)).toBeInTheDocument()
    })

    it('sem permissão de agenda, não mostra o link', async () => {
        app(['cliente.ver'])
        await abrir('/conta')
        expect(await screen.findByRole('heading', { name: 'E-mails' })).toBeInTheDocument()
        expect(screen.queryByRole('heading', { name: 'Agenda no seu calendário' })).not.toBeInTheDocument()
    })
    describe('notificações neste navegador (F12-20, F12-21, F12-25)', () => {
        const comNotificacao = (permissao: NotificationPermission, resposta: NotificationPermission = permissao) => {
            const pedir = vi.fn(async () => resposta)
            vi.stubGlobal(
                'Notification',
                class {
                    static permission = permissao
                    static requestPermission = pedir
                },
            )
            return pedir
        }
        afterEach(() => localStorage.clear())

        it('ligar pede a permissão e fica ligado; desligar desliga', async () => {
            const pedir = comNotificacao('default', 'granted')
            app(['tarefa.ver'])
            await abrir('/conta')
            const usuario = userEvent.setup()
            const marcador = await screen.findByRole('checkbox', { name: 'Notificações neste navegador' })
            expect(marcador).not.toBeChecked()
            await usuario.click(marcador)
            expect(pedir).toHaveBeenCalled()
            await vi.waitFor(() => expect(marcador).toBeChecked())
            await usuario.click(marcador)
            await vi.waitFor(() => expect(marcador).not.toBeChecked())
        })

        it('bloqueada no navegador, fica desligada e explica como liberar', async () => {
            comNotificacao('denied')
            app(['tarefa.ver'])
            await abrir('/conta')
            const marcador = await screen.findByRole('checkbox', { name: 'Notificações neste navegador' })
            expect(marcador).toBeDisabled()
            expect(marcador).not.toBeChecked()
            expect(marcador).toHaveAccessibleDescription(/O navegador bloqueou as notificações do Acordu/)
        })

        it('negar na hora deixa desligada, avisa e mostra como liberar', async () => {
            comNotificacao('default', 'denied')
            app(['tarefa.ver'])
            await abrir('/conta')
            const usuario = userEvent.setup()
            const marcador = await screen.findByRole('checkbox', { name: 'Notificações neste navegador' })
            await usuario.click(marcador)
            expect(await screen.findByText(/O navegador bloqueou as notificações. Veja abaixo/)).toBeInTheDocument()
            expect(marcador).not.toBeChecked()
            expect(screen.getByText(/clique no cadeado ao lado do endereço/)).toBeInTheDocument()
        })

        it('navegador sem suporte: a opção não aparece', async () => {
            vi.stubGlobal('Notification', undefined)
            delete (window as { Notification?: unknown }).Notification
            app(['tarefa.ver'])
            await abrir('/conta')
            await screen.findByRole('heading', { name: 'E-mails' })
            expect(screen.queryByRole('checkbox', { name: 'Notificações neste navegador' })).not.toBeInTheDocument()
        })
    })
    describe('lembrete do cronômetro (F12-13, F12-16)', () => {
        afterEach(() => localStorage.clear())

        it('vem desligado e guarda o intervalo escolhido neste navegador', async () => {
            app(['tarefa.ver', 'apontamento.criar'])
            await abrir('/conta')
            const usuario = userEvent.setup()
            const campo = await screen.findByRole('combobox', { name: 'Lembrete do cronômetro' })
            expect(campo).toHaveAttribute('placeholder', 'Desligado')
            expect(campo).toHaveValue('')
            await usuario.click(campo)
            await usuario.click(await screen.findByRole('option', { name: 'Depois de 30 min sem cronômetro' }))
            expect(await screen.findByText('Lembrete a cada 30 min sem cronômetro.')).toBeInTheDocument()
            expect(
                [...Array(localStorage.length).keys()].map((i) => localStorage.getItem(localStorage.key(i)!)),
            ).toContain('30')
        })

        it('sem apontamento.criar, não aparece', async () => {
            app(['tarefa.ver'])
            await abrir('/conta')
            await screen.findByRole('heading', { name: 'E-mails' })
            expect(screen.queryByRole('combobox', { name: 'Lembrete do cronômetro' })).not.toBeInTheDocument()
        })
    })
})
