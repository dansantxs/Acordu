import { describe, expect, it } from 'vitest'
import { formatarCep, formatarDocumento, formatarTelefone, posicaoDoCursor } from './mascaras'

describe('máscaras', () => {
    it.each([
        ['', ''],
        ['529', '529'],
        ['5299', '529.9'],
        ['52998224725', '529.982.247-25'],
        ['112223330001', '11.222.333/0001'],
        ['11222333000181', '11.222.333/0001-81'],
        ['11.222.333/0001-81999', '11.222.333/0001-81'],
    ])('documento %s → %s', (entrada, saida) => expect(formatarDocumento(entrada)).toBe(saida))

    it.each([
        ['', ''],
        ['1', '(1'],
        ['18', '(18'],
        ['183', '(18) 3'],
        ['1833330000', '(18) 3333-0000'],
        ['18999990000', '(18) 99999-0000'],
        ['+1 415 555 0100', '+1 415 555 0100'],
    ])('telefone %s → %s', (entrada, saida) => expect(formatarTelefone(entrada)).toBe(saida))

    it.each([
        ['197', '197'],
        ['197000', '19700-0'],
        ['19700000', '19700-000'],
        ['19700-0001', '19700-000'],
    ])('CEP %s → %s', (entrada, saida) => expect(formatarCep(entrada)).toBe(saida))

    it('o cursor fica depois do mesmo número de dígitos', () => {
        expect(posicaoDoCursor('529.982.247-25', 3)).toBe(3)
        expect(posicaoDoCursor('529.982.247-25', 4)).toBe(5)
        expect(posicaoDoCursor('(18) 3333-0000', 2)).toBe(3)
        expect(posicaoDoCursor('19700-000', 0)).toBe(0)
        expect(posicaoDoCursor('19700-000', 99)).toBe(9)
    })
})
