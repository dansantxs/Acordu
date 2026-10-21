import { formatarDuracao, lerDuracao } from './duracao'

describe('formatarDuracao', () => {
    it.each([
        [0, '0min'],
        [45, '45min'],
        [60, '1h'],
        [90, '1h30'],
        [125, '2h05'],
        [2400, '40h'],
        [-75, '−1h15'],
    ])('%i minutos → %s', (minutos, esperado) => {
        expect(formatarDuracao(minutos)).toBe(esperado)
    })

    it('recusa minutos fracionados', () => {
        expect(() => formatarDuracao(1.5)).toThrow(RangeError)
    })
})

describe('lerDuracao', () => {
    it('aceita os jeitos comuns de escrever', () => {
        expect(lerDuracao('1h30')).toBe(90)
        expect(lerDuracao('1h')).toBe(60)
        expect(lerDuracao('2h05min')).toBe(125)
        expect(lerDuracao('1:30')).toBe(90)
        expect(lerDuracao('90')).toBe(90)
        expect(lerDuracao('45min')).toBe(45)
        expect(lerDuracao(' 1,5h ')).toBe(90)
        expect(lerDuracao('0')).toBeNull()
        expect(lerDuracao('uma hora')).toBeNull()
        expect(lerDuracao('')).toBeNull()
    })
})
