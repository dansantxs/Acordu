import { describe, expect, it } from 'vitest'
import { dataCurta, diaPorExtenso, hojeNoFuso, segundaDaSemana, somarDias } from './datas'

describe('datas', () => {
    it('hoje é o do fuso do tenant', () => {
        // 01:30 UTC ainda é dia anterior em São Paulo.
        expect(hojeNoFuso('America/Sao_Paulo', new Date('2026-10-21T01:30:00Z'))).toBe('2026-10-20')
        expect(hojeNoFuso('UTC', new Date('2026-10-21T01:30:00Z'))).toBe('2026-10-21')
    })

    it('mostra o ano só quando muda', () => {
        expect(dataCurta('2026-10-01', '2026-10-20')).toBe('01/10')
        expect(dataCurta('2025-12-31', '2026-10-20')).toBe('31/12/25')
    })
})

describe('semana', () => {
    it('acha a segunda-feira e soma dias', () => {
        expect(segundaDaSemana('2026-10-21')).toBe('2026-10-19')
        expect(segundaDaSemana('2026-10-19')).toBe('2026-10-19')
        expect(segundaDaSemana('2026-10-25')).toBe('2026-10-19')
        expect(somarDias('2026-10-30', 3)).toBe('2026-11-02')
        expect(diaPorExtenso('2026-10-20')).toBe('terça-feira, 20/10')
    })
})
