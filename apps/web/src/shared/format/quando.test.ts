import { describe, expect, it } from 'vitest'
import { quando } from './quando'

const agora = new Date('2026-10-15T15:00:00Z') // 12:00 em São Paulo
const fuso = 'America/Sao_Paulo'

describe('quando', () => {
    it.each([
        ['2026-10-15T14:59:30Z', 'agora'],
        ['2026-10-15T14:55:00Z', 'há 5 min'],
        ['2026-10-15T12:00:00Z', 'há 3 h'],
        ['2026-10-15T02:00:00Z', 'ontem'], // 23:00 do dia 14 em São Paulo, embora seja dia 15 em UTC
        ['2026-10-12T15:00:00Z', '12/10'],
    ])('%s → %s', (instante, esperado) => {
        expect(quando(instante, fuso, agora)).toBe(esperado)
    })
})
