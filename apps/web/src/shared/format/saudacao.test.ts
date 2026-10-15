import { describe, expect, it } from 'vitest'
import { diaPorExtenso, saudacao } from './saudacao'

const fuso = 'America/Sao_Paulo'

describe('saudação', () => {
    it.each([
        ['2026-10-15T11:59:00Z', 'Bom dia'], // 08:59
        ['2026-10-15T15:00:00Z', 'Boa tarde'], // 12:00
        ['2026-10-15T21:00:00Z', 'Boa noite'], // 18:00
        ['2026-10-16T02:30:00Z', 'Boa noite'], // 23:30 do dia 15
    ])('%s → %s', (instante, esperado) => {
        expect(saudacao(fuso, new Date(instante))).toBe(esperado)
    })

    it('dia por extenso no fuso do tenant, sem "-feira"', () => {
        expect(diaPorExtenso(fuso, new Date('2026-10-16T02:30:00Z'))).toBe('Quinta, 15 de outubro')
    })
})
