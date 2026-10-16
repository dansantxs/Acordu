import { describe, expect, it } from 'vitest'
import { dataHora, prazoRelativo } from './prazo'

const agora = new Date('2026-10-16T15:00:00Z')
const fuso = 'America/Sao_Paulo'

describe('prazo relativo', () => {
    it.each([
        ['2026-10-16T15:20:00Z', 'vence em 20 min'],
        ['2026-10-16T18:30:00Z', 'vence em 3 h'],
        ['2026-10-18T17:00:00Z', 'vence 18/10 14:00'],
        ['2026-10-16T14:59:40Z', 'venceu há 1 min'],
        ['2026-10-16T13:00:00Z', 'venceu há 2 h'],
        ['2026-10-15T10:00:00Z', 'venceu há 1 dia'],
        ['2026-10-12T10:00:00Z', 'venceu há 4 dias'],
    ])('%s → %s', (instante, esperado) => expect(prazoRelativo(instante, fuso, agora)).toBe(esperado))

    it('data e hora no fuso do tenant', () => expect(dataHora('2026-10-17T02:30:00Z', fuso)).toBe('16/10 23:30'))
})
