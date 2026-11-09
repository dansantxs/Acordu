import { describe, expect, it } from 'vitest'
import { inicioPelaHora } from './cronometro'

describe('inicioPelaHora', () => {
    const agora = new Date(2026, 10, 9, 10, 0)

    it('horário já passado é de hoje', () => {
        expect(inicioPelaHora('08:40', agora)).toEqual(new Date(2026, 10, 9, 8, 40))
    })

    it('horário que ainda não chegou é de ontem', () => {
        expect(inicioPelaHora('22:30', agora)).toEqual(new Date(2026, 10, 8, 22, 30))
    })

    it.each(['', '8h40', '25:00', '10:61'])('"%s" é inválido', (hora) => {
        expect(inicioPelaHora(hora, agora)).toBeNull()
    })
})
