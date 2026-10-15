import { formatarDuracao } from './duracao'

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
