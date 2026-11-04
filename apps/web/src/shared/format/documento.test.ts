import { describe, expect, it } from 'vitest'
import { cnpjCompleto, documentoValido, validarDocumento } from './documento'

// Mesma regra do `Documento` da API (spec do F10, F10-22 e F10-25).
describe('documento', () => {
    it.each([
        ['529.982.247-25', true],
        ['529.982.247-24', false],
        ['111.111.111-11', false],
        ['11.222.333/0001-81', true],
        ['11.222.333/0001-80', false],
        ['00.000.000/0000-00', false],
        // Exemplo da Receita para o CNPJ alfanumérico (código ASCII menos 48).
        ['12.ABC.345/01DE-35', true],
        ['12.abc.345/01de-35', true],
        ['12.ABC.345/01DE-36', false],
        ['1234567', false],
    ])('%s válido: %s', (texto, esperado) => expect(documentoValido(texto)).toBe(esperado))

    it('o campo aceita vazio e explica o inválido', () => {
        expect(validarDocumento('')).toBeNull()
        expect(validarDocumento('529.982.247-25')).toBeNull()
        expect(validarDocumento('529.982.247-24')).toBe('CPF ou CNPJ inválido.')
    })

    it('só CNPJ completo e válido pode ser consultado; CPF nunca', () => {
        expect(cnpjCompleto('11.222.333/0001-81')).toBe('11222333000181')
        expect(cnpjCompleto('12.abc.345/01de-35')).toBe('12ABC34501DE35')
        expect(cnpjCompleto('529.982.247-25')).toBeNull()
        expect(cnpjCompleto('11.222.333/0001-80')).toBeNull()
        expect(cnpjCompleto('11.222.333/0001')).toBeNull()
    })
})
