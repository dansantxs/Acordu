import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { desligarNotificacoes, estadoNotificacoes, ligarNotificacoes, notificar } from './navegador'

class NotificacaoFalsa {
    static permission: NotificationPermission = 'default'
    static requestPermission = vi.fn(async () => NotificacaoFalsa.permission)
    static criadas: NotificacaoFalsa[] = []
    onclick: (() => void) | null = null
    close = vi.fn()
    constructor(
        public titulo: string,
        public opcoes: NotificationOptions,
    ) {
        NotificacaoFalsa.criadas.push(this)
    }
}

const AGORA = new Date('2026-11-09T13:00:00Z').getTime()
const aviso = (id: string, minutosAtras = 1) => ({
    id,
    titulo: 'Nova solicitação #1042',
    caminho: '/solicitacoes/s1',
    criadoEm: new Date(AGORA - minutosAtras * 60_000).toISOString(),
})

describe('notificação do navegador', () => {
    beforeEach(() => {
        localStorage.clear()
        NotificacaoFalsa.permission = 'default'
        NotificacaoFalsa.criadas = []
        NotificacaoFalsa.requestPermission.mockClear()
        vi.stubGlobal('Notification', NotificacaoFalsa)
    })
    afterEach(() => vi.unstubAllGlobals())

    it('ligar pede a permissão; aceita, fica ligada (F12-20)', async () => {
        expect(estadoNotificacoes()).toBe('desligada')
        NotificacaoFalsa.permission = 'granted'
        expect(await ligarNotificacoes(AGORA - 60 * 60_000)).toBe('ligada')
        expect(estadoNotificacoes()).toBe('ligada')
        desligarNotificacoes()
        expect(estadoNotificacoes()).toBe('desligada')
    })

    it('negada no navegador fica negada (F12-21)', async () => {
        NotificacaoFalsa.requestPermission.mockImplementationOnce(async () => 'denied')
        expect(await ligarNotificacoes()).toBe('negada')
        NotificacaoFalsa.permission = 'denied'
        expect(estadoNotificacoes()).toBe('negada')
    })

    it('sem a API, não há suporte (F12-25)', () => {
        vi.stubGlobal('Notification', undefined)
        // `in window` continua verdadeiro com undefined: remove de vez.
        delete (window as { Notification?: unknown }).Notification
        expect(estadoNotificacoes()).toBe('sem-suporte')
    })

    describe('ligada', () => {
        beforeEach(async () => {
            NotificacaoFalsa.permission = 'granted'
            await ligarNotificacoes(AGORA - 60 * 60_000)
        })

        it('mostra o aviso novo uma vez, com o tag do aviso (F12-22)', () => {
            expect(notificar(aviso('n1'), vi.fn(), AGORA)).toBe(true)
            expect(NotificacaoFalsa.criadas).toHaveLength(1)
            expect(NotificacaoFalsa.criadas[0]!.titulo).toBe('Nova solicitação #1042')
            expect(NotificacaoFalsa.criadas[0]!.opcoes.tag).toBe('n1')
            // A próxima consulta (ou outra aba, que lê o mesmo armazenamento) não repete (F12-24).
            expect(notificar(aviso('n1'), vi.fn(), AGORA + 60_000)).toBe(false)
            expect(NotificacaoFalsa.criadas).toHaveLength(1)
        })

        it('clique foca a janela, abre o item e fecha a notificação (F12-23)', () => {
            const abrir = vi.fn()
            const foco = vi.spyOn(window, 'focus').mockImplementation(() => undefined)
            notificar(aviso('n2'), abrir, AGORA)
            NotificacaoFalsa.criadas[0]!.onclick!()
            expect(foco).toHaveBeenCalled()
            expect(abrir).toHaveBeenCalledWith('/solicitacoes/s1')
            expect(NotificacaoFalsa.criadas[0]!.close).toHaveBeenCalled()
        })

        it('aviso velho (mais de 10 min) ou de antes de ligar não vira notificação', () => {
            expect(notificar(aviso('n3', 11), vi.fn(), AGORA)).toBe(false)
            expect(notificar(aviso('n4', 61), vi.fn(), AGORA)).toBe(false)
            expect(NotificacaoFalsa.criadas).toHaveLength(0)
        })
    })

    it('desligada, nada aparece', () => {
        NotificacaoFalsa.permission = 'granted'
        expect(notificar(aviso('n5'), vi.fn(), AGORA)).toBe(false)
        expect(NotificacaoFalsa.criadas).toHaveLength(0)
    })
})
