import { describe, expect, it } from 'vitest'
import { inicioPelaHora } from './cronometro'

describe('inicioPelaHora', () => {
    // 09/11/2026 10:00 em São Paulo (UTC-3).
    const agora = new Date('2026-11-09T13:00:00Z')
    const SP = 'America/Sao_Paulo'

    it('horário já passado é de hoje, no fuso da empresa', () => {
        expect(inicioPelaHora('08:40', SP, agora)?.toISOString()).toBe('2026-11-09T11:40:00.000Z')
    })

    it('horário que ainda não chegou é de ontem', () => {
        expect(inicioPelaHora('22:30', SP, agora)?.toISOString()).toBe('2026-11-09T01:30:00.000Z')
    })

    it('outro fuso da empresa muda o instante, não o relógio do aparelho', () => {
        expect(inicioPelaHora('08:40', 'Europe/Lisbon', agora)?.toISOString()).toBe('2026-11-09T08:40:00.000Z')
    })

    it.each(['', '8h40', '25:00', '10:61'])('"%s" é inválido', (hora) => {
        expect(inicioPelaHora(hora, SP, agora)).toBeNull()
    })
})
