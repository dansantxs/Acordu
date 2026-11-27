import { describe, expect, it } from 'vitest'
import { fornecedorDe } from '@/shared/config/fornecedor'
import { lerTextoLegal } from './textosLegais'

// ADR 0017: os textos legais têm o marcador do fornecedor; a impressão confere o corpo com ele, e os blocos têm o nome.
describe('textos legais: fornecedor', () => {
    it('TextoLegal_OutroFornecedor_NomeNosBlocosSemMarcador', () => {
        const texto = lerTextoLegal(
            '---\nversao: 1\nvigencia: 2026-11-16\n---\n\n{{A fornecedor}} fornece o Acordu.\n',
            fornecedorDe('Grupo Exemplo', 'o'),
        )
        expect(texto.blocos).toEqual([{ tipo: 'paragrafo', texto: 'O Grupo Exemplo fornece o Acordu.' }])
        expect(texto.corpo).toBe('{{A fornecedor}} fornece o Acordu.')
    })
})
