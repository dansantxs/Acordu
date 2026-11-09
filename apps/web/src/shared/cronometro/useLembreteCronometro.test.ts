import { afterEach, describe, expect, it, vi } from 'vitest'
import { definirLembrete } from './lembrete'
import { criarConferenciaLembrete, mostrarLembrete } from './useLembreteCronometro'
import { filaDeAvisos } from '../avisos/avisos'

const MIN = 60_000
// Segunda, 09/11/2026, 08:00 em São Paulo.
const ABRE = new Date('2026-11-09T11:00:00Z').getTime()
const calendario = {
    fusoHorario: 'America/Sao_Paulo',
    expediente: [{ diaSemana: 'SEGUNDA' as const, inicio: '08:00', fim: '18:00' }],
    feriados: [],
}

function cenario({ cronometro = false, cal = calendario, abertoEm = ABRE } = {}) {
    const lembrar = vi.fn()
    let ha = cronometro
    const haCronometro = vi.fn(async () => ha)
    const conferir = criarConferenciaLembrete({
        escopo: 't1:ana',
        abertoEm,
        calendario: async () => cal,
        haCronometro,
        lembrar,
    })
    /** Confere minuto a minuto até `minutos` depois da abertura, como o intervalo do hook. */
    const ate = async (minutos: number, desde = 1) => {
        for (let m = desde; m <= minutos; m++) await conferir(ABRE + m * MIN)
    }
    return { lembrar, ate, conferir, haCronometro, mudarCronometro: (v: boolean) => (ha = v) }
}

describe('conferência do lembrete', () => {
    afterEach(() => localStorage.clear())

    it('ligado, lembra depois do intervalo e não de novo antes de outro (F12-13, F12-15)', async () => {
        definirLembrete('t1:ana', 15, ABRE - 60 * MIN)
        const { lembrar, ate } = cenario()
        await ate(14)
        expect(lembrar).not.toHaveBeenCalled()
        await ate(15, 15)
        expect(lembrar).toHaveBeenCalledTimes(1)
        expect(lembrar).toHaveBeenLastCalledWith(ABRE + 15 * MIN)
        await ate(29, 16)
        expect(lembrar).toHaveBeenCalledTimes(1)
        await ate(30, 30)
        expect(lembrar).toHaveBeenCalledTimes(2)
    })

    it('cronômetro rodando zera a contagem (F12-15)', async () => {
        definirLembrete('t1:ana', 15, ABRE - 60 * MIN)
        const { lembrar, ate, mudarCronometro } = cenario({ cronometro: true })
        await ate(20)
        mudarCronometro(false)
        await ate(34, 21)
        expect(lembrar).not.toHaveBeenCalled()
        await ate(35, 35)
        expect(lembrar).toHaveBeenCalledTimes(1)
    })

    it('fora do expediente, nada, e o cronômetro nem é consultado (F12-14)', async () => {
        definirLembrete('t1:ana', 15, ABRE - 60 * MIN)
        const { lembrar, ate, haCronometro } = cenario({ cal: { ...calendario, expediente: [] } })
        await ate(60)
        expect(lembrar).not.toHaveBeenCalled()
        expect(haCronometro).not.toHaveBeenCalled()
    })

    it('duas abas abertas em horas diferentes lembram uma vez só (F12-15)', async () => {
        definirLembrete('t1:ana', 15, ABRE - 60 * MIN)
        const primeira = cenario()
        const segunda = cenario({ abertoEm: ABRE + 2 * MIN })
        for (let m = 1; m <= 25; m++) {
            await primeira.conferir(ABRE + m * MIN)
            await segunda.conferir(ABRE + m * MIN + 30_000)
        }
        expect(primeira.lembrar.mock.calls.length + segunda.lembrar.mock.calls.length).toBe(1)
    })

    it('ligar no meio do dia: o primeiro aviso vem um intervalo depois de ligar', async () => {
        definirLembrete('t1:ana', 15, ABRE + 30 * MIN)
        const { lembrar, ate } = cenario()
        await ate(44)
        expect(lembrar).not.toHaveBeenCalled()
        await ate(45, 45)
        expect(lembrar).toHaveBeenCalledTimes(1)
    })

    it('desligado (padrão), nada (F12-16)', async () => {
        const { lembrar, ate } = cenario()
        await ate(60)
        expect(lembrar).not.toHaveBeenCalled()
    })

    it('o lembrete é o aviso com "Minhas tarefas" e, ligada, a notificação do sistema (F12-13)', () => {
        const fila = vi.spyOn(filaDeAvisos, 'add')
        const criadas: { titulo: string; opcoes: NotificationOptions; onclick: (() => void) | null }[] = []
        vi.stubGlobal(
            'Notification',
            class {
                static permission = 'granted'
                onclick: (() => void) | null = null
                close = vi.fn()
                constructor(
                    public titulo: string,
                    public opcoes: NotificationOptions,
                ) {
                    criadas.push(this)
                }
            },
        )
        localStorage.setItem('acordu:notificacoes-navegador:t1:ana', String(ABRE - 60 * MIN))
        const abrir = vi.fn()
        try {
            mostrarLembrete('t1:ana', ABRE + 15 * MIN, abrir)
            const aviso = fila.mock.calls[0]![0] as { texto: string; acao: { rotulo: string; executar: () => void } }
            expect(aviso.texto).toBe('Nenhum cronômetro rodando. Começar agora?')
            expect(aviso.acao.rotulo).toBe('Minhas tarefas')
            aviso.acao.executar()
            expect(abrir).toHaveBeenCalledWith('/tarefas?responsavel=eu')
            expect(criadas).toHaveLength(1)
            expect(criadas[0]!.titulo).toBe('Nenhum cronômetro rodando')
            expect(criadas[0]!.opcoes.body).toBe('Começar agora?')
        } finally {
            fila.mockRestore()
            vi.unstubAllGlobals()
        }
    })
})
