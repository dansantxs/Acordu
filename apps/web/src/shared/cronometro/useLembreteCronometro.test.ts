import { afterEach, describe, expect, it, vi } from 'vitest'
import { definirLembrete } from './lembrete'
import { criarConferenciaLembrete } from './useLembreteCronometro'

const MIN = 60_000
// Segunda, 09/11/2026, 08:00 em São Paulo.
const ABRE = new Date('2026-11-09T11:00:00Z').getTime()
const calendario = {
    fusoHorario: 'America/Sao_Paulo',
    expediente: [{ diaSemana: 'SEGUNDA' as const, inicio: '08:00', fim: '18:00' }],
    feriados: [],
}

function cenario({ cronometro = false, cal = calendario } = {}) {
    const lembrar = vi.fn()
    let ha = cronometro
    const conferir = criarConferenciaLembrete({
        escopo: 't1:ana',
        abertoEm: ABRE,
        calendario: async () => cal,
        haCronometro: async () => ha,
        lembrar,
    })
    /** Confere minuto a minuto até `minutos` depois da abertura, como o intervalo do hook. */
    const ate = async (minutos: number, desde = 1) => {
        for (let m = desde; m <= minutos; m++) await conferir(ABRE + m * MIN)
    }
    return { lembrar, ate, mudarCronometro: (v: boolean) => (ha = v) }
}

describe('conferência do lembrete', () => {
    afterEach(() => localStorage.clear())

    it('ligado, lembra depois do intervalo e não de novo antes de outro (F12-13, F12-15)', async () => {
        definirLembrete('t1:ana', 15)
        const { lembrar, ate } = cenario()
        await ate(14)
        expect(lembrar).not.toHaveBeenCalled()
        await ate(15, 15)
        expect(lembrar).toHaveBeenCalledTimes(1)
        expect(lembrar).toHaveBeenLastCalledWith(ABRE + 15 * MIN, 15)
        await ate(29, 16)
        expect(lembrar).toHaveBeenCalledTimes(1)
        await ate(30, 30)
        expect(lembrar).toHaveBeenCalledTimes(2)
    })

    it('cronômetro rodando zera a contagem', async () => {
        definirLembrete('t1:ana', 15)
        const { lembrar, ate, mudarCronometro } = cenario({ cronometro: true })
        await ate(20)
        mudarCronometro(false)
        await ate(34, 21)
        expect(lembrar).not.toHaveBeenCalled()
        await ate(35, 35)
        expect(lembrar).toHaveBeenCalledTimes(1)
    })

    it('fora do expediente, nada (F12-14)', async () => {
        definirLembrete('t1:ana', 15)
        const { lembrar, ate } = cenario({ cal: { ...calendario, expediente: [] } })
        await ate(60)
        expect(lembrar).not.toHaveBeenCalled()
    })

    it('desligado (padrão), nada (F12-16)', async () => {
        const { lembrar, ate } = cenario()
        await ate(60)
        expect(lembrar).not.toHaveBeenCalled()
    })
})
