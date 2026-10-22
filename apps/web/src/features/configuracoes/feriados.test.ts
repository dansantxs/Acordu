import { describe, expect, it } from 'vitest'
import { feriadosNacionais, pascoa } from './feriados'

describe('feriados nacionais', () => {
    it('calcula a Páscoa e a Paixão de Cristo', () => {
        expect(pascoa(2026)).toBe('2026-04-05')
        expect(pascoa(2027)).toBe('2027-03-28')
        expect(pascoa(2025)).toBe('2025-04-20')
        expect(feriadosNacionais(2026).find((f) => f.nome === 'Paixão de Cristo')?.data).toBe('2026-04-03')
    })

    it('traz os dez feriados nacionais do ano, sem ponto facultativo', () => {
        const datas = feriadosNacionais(2026).map((f) => f.data)
        expect(datas).toHaveLength(10)
        expect(datas).toContain('2026-11-20')
        expect(datas).not.toContain('2026-02-17')
    })
})
