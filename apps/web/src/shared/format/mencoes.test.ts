import { describe, expect, it } from 'vitest'
import { mencaoEmDigitacao, partesDoTexto, textoComTokens } from './mencoes'

const RAFAEL = '0199f0a0-0000-7000-8000-000000000002'
const SAIU = '0199f0a0-0000-7000-8000-000000000009'

describe('menções', () => {
    it('troca os tokens pelos nomes e marca quem saiu da equipe', () => {
        const nomes = new Map([[RAFAEL, 'Rafael Moura']])
        expect(partesDoTexto(`Oi @{${RAFAEL}}, veja com @{${SAIU}}.`, nomes)).toEqual([
            { tipo: 'texto', texto: 'Oi ' },
            { tipo: 'mencao', id: RAFAEL, nome: 'Rafael Moura' },
            { tipo: 'texto', texto: ', veja com ' },
            { tipo: 'mencao', id: SAIU, nome: null },
            { tipo: 'texto', texto: '.' },
        ])
        expect(partesDoTexto('sem menção', nomes)).toEqual([{ tipo: 'texto', texto: 'sem menção' }])
    })

    it('monta os tokens no envio, sem confundir nomes que começam igual', () => {
        const ana = { id: 'a', nome: 'Ana' }
        const anaPaula = { id: 'b', nome: 'Ana Paula' }
        expect(textoComTokens('@Ana Paula e @Ana, vejam', [ana, anaPaula])).toBe('@{b} e @{a}, vejam')
        expect(textoComTokens('escrevi @Ana à mão', [])).toBe('escrevi @Ana à mão')
    })

    it('só abre a lista depois de um @ no começo ou após espaço', () => {
        expect(mencaoEmDigitacao('Oi @raf')).toBe('raf')
        expect(mencaoEmDigitacao('@')).toBe('')
        expect(mencaoEmDigitacao('ana@exemplo')).toBeNull()
        expect(mencaoEmDigitacao('Oi @rafael ')).toBeNull()
    })
})
