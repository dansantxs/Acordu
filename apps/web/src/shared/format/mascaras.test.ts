import { describe, expect, it } from 'vitest'
import { caracteresDoDocumento, formatarCep, formatarDocumento, formatarTelefone, posicaoDoCursor } from './mascaras'

describe('máscaras', () => {
    it.each([
        ['', ''],
        ['529', '529'],
        ['5299', '529.9'],
        ['52998224725', '529.982.247-25'],
        ['112223330001', '11.222.333/0001'],
        ['11222333000181', '11.222.333/0001-81'],
        ['11.222.333/0001-81999', '11.222.333/0001-81'],
        // CNPJ alfanumérico (F10-24, F10-27): letra só nas 12 primeiras posições, em maiúsculas.
        ['12abc34501de35', '12.ABC.345/01DE-35'],
        ['12.ABC.345/01DE-35', '12.ABC.345/01DE-35'],
        ['12A', '12.A'],
        ['12ABC34501DEXY35', '12.ABC.345/01DE-35'],
        ['529A98224725', '52.9A9.822/4725'],
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

    it('no documento, letras contam para o cursor e só entram nas 12 primeiras posições', () => {
        const alfanumerico = (c: string) => caracteresDoDocumento(c).length === 1
        expect(posicaoDoCursor('12.ABC.345/01DE-35', 4, alfanumerico)).toBe(5)
        expect(caracteresDoDocumento('12.abc.345/01de-3x5')).toBe('12ABC34501DE35')
        expect(caracteresDoDocumento('529.982.247-25')).toBe('52998224725')
    })
})
