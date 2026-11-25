import { describe, expect, it } from 'vitest'
import { linkWhatsApp } from './whatsapp'

describe('linkWhatsApp', () => {
    it.each([
        ['(14) 99999-0000', 'https://wa.me/5514999990000'],
        ['14999990000', 'https://wa.me/5514999990000'],
        ['+55 14 99999-0000', 'https://wa.me/5514999990000'],
        ['5514999990000', 'https://wa.me/5514999990000'],
        ['(14) 3222-0101', 'https://wa.me/551432220101'],
        ['551432220101', 'https://wa.me/551432220101'],
        ['(014) 99999-0000', 'https://wa.me/5514999990000'],
        ['0055 14 99999-0000', 'https://wa.me/5514999990000'],
        ['  (14) 99999-0000  ', 'https://wa.me/5514999990000'],
    ])('LinkWhatsApp_TelefoneBrasileiro_%s_EnderecoComDdi55', (telefone, esperado) => {
        expect(linkWhatsApp(telefone)).toBe(esperado)
    })

    it('LinkWhatsApp_DddCincoCincoSemDdi_GanhaO55', () => {
        expect(linkWhatsApp('(55) 99999-0000')).toBe('https://wa.me/5555999990000')
    })

    it('LinkWhatsApp_InternacionalComMais_RespeitaODdi', () => {
        expect(linkWhatsApp('+351 912 345 678')).toBe('https://wa.me/351912345678')
        expect(linkWhatsApp('+1 (415) 555-2671')).toBe('https://wa.me/14155552671')
    })

    it('LinkWhatsApp_InternacionalNosLimitesDoE164_AceitaDe8a15Digitos', () => {
        expect(linkWhatsApp('+12345678')).toBe('https://wa.me/12345678')
        expect(linkWhatsApp('+123456789012345')).toBe('https://wa.me/123456789012345')
        expect(linkWhatsApp('+1234567890123456')).toBeNull()
    })

    it.each([['0800 123 4567'], ['0300 123 4567'], ['4004-1234'], ['0800 4004 1234']])(
        'LinkWhatsApp_NumeroNaoGeografico_%s_Nulo',
        (telefone) => {
            expect(linkWhatsApp(telefone)).toBeNull()
        },
    )

    it('LinkWhatsApp_EstrangeiroSemMais_Nulo', () => {
        expect(linkWhatsApp('14155552671')).toBeNull()
        expect(linkWhatsApp('1 415 555 2671')).toBeNull()
    })

    it.each([['(20) 99999-0000'], ['(23) 3222-0101'], ['(25) 99999-0000'], ['+55 20 99999-0000'], ['552399999000']])(
        'LinkWhatsApp_DddInexistente_%s_Nulo',
        (telefone) => {
            expect(linkWhatsApp(telefone)).toBeNull()
        },
    )

    it.each([['(14) 89999-0000'], ['+55 14 89999-0000'], ['5514899990000']])(
        'LinkWhatsApp_CelularSemONove_%s_Nulo',
        (telefone) => {
            expect(linkWhatsApp(telefone)).toBeNull()
        },
    )

    it.each([
        ['(11) 98765-4321', 'https://wa.me/5511987654321'],
        ['(99) 3222-0101', 'https://wa.me/559932220101'],
        ['+55 21 2222-0101', 'https://wa.me/552122220101'],
    ])('LinkWhatsApp_DddValido_%s_Endereco', (telefone, esperado) => {
        expect(linkWhatsApp(telefone)).toBe(esperado)
    })

    it.each([[null], [undefined], [''], ['   ']])('LinkWhatsApp_SemTelefone_%s_Nulo', (telefone) => {
        expect(linkWhatsApp(telefone)).toBeNull()
    })

    it.each([['99999-0000'], ['1234'], ['(00) 0000-0101'], ['+55 14 9999'], ['+123'], ['55149999900001']])(
        'LinkWhatsApp_NumeroInvalido_%s_Nulo',
        (telefone) => {
            expect(linkWhatsApp(telefone)).toBeNull()
        },
    )

    it.each([['(14) 3222-0101 ramal 5'], ['ligar à tarde'], ['14 9999O-0000']])(
        'LinkWhatsApp_ComLetras_%s_Nulo',
        (telefone) => {
            expect(linkWhatsApp(telefone)).toBeNull()
        },
    )
})
