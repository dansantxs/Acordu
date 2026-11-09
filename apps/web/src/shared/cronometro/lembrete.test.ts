import { afterEach, describe, expect, it } from 'vitest'
import { definirLembrete, deveLembrar, intervaloLembrete } from './lembrete'

const MIN = 60_000
const AGORA = 1_000 * MIN

describe('lembrete do cronômetro', () => {
    afterEach(() => localStorage.clear())

    it('desligado por padrão e guardado por pessoa (F12-16)', () => {
        expect(intervaloLembrete('t1:ana')).toBeNull()
        definirLembrete('t1:ana', 30)
        expect(intervaloLembrete('t1:ana')).toBe(30)
        expect(intervaloLembrete('t1:bruno')).toBeNull()
        definirLembrete('t1:ana', null)
        expect(intervaloLembrete('t1:ana')).toBeNull()
    })

    it('lembra depois do intervalo sem cronômetro, no expediente (F12-13)', () => {
        const base = { agora: AGORA, intervaloMinutos: 30, inicioDoExpediente: AGORA - 120 * MIN }
        expect(deveLembrar({ ...base, referencias: [AGORA - 31 * MIN, null, null] })).toBe(true)
        expect(deveLembrar({ ...base, referencias: [AGORA - 29 * MIN, null, null] })).toBe(false)
    })

    it('fora do expediente, não (F12-14)', () => {
        expect(
            deveLembrar({ agora: AGORA, intervaloMinutos: 15, inicioDoExpediente: null, referencias: [0, null, null] }),
        ).toBe(false)
    })

    it('conta do mais recente: início do expediente, cronômetro visto ou último lembrete (F12-15)', () => {
        const base = { agora: AGORA, intervaloMinutos: 30, referencias: [0] as (number | null)[] }
        expect(deveLembrar({ ...base, inicioDoExpediente: AGORA - 10 * MIN })).toBe(false)
        expect(deveLembrar({ ...base, inicioDoExpediente: 0, referencias: [0, AGORA - 5 * MIN] })).toBe(false)
        expect(deveLembrar({ ...base, inicioDoExpediente: 0, referencias: [0, null, AGORA - 20 * MIN] })).toBe(false)
    })
})
