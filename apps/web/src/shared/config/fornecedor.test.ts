import { describe, expect, it } from 'vitest'
import { faleComOFornecedor, fornecedorDe, textoDoFornecedor } from './fornecedor'

const MODELO =
    '{{A fornecedor}} convidou; fale com {{a fornecedor}}, peça {{à fornecedor}}, área {{da fornecedor}}, ' +
    '{{na fornecedor}}, {{pela fornecedor}}; {{fornecedor}}.'

// ADR 0017: o nome do fornecedor nos textos vem do build, com o artigo configurado, como na API (OpcoesFornecedor).
describe('fornecedor nos textos', () => {
    it('Texto_SemConfiguracao_NebrysNoFeminino', () => {
        expect(textoDoFornecedor(MODELO, fornecedorDe())).toBe(
            'A Nebrys convidou; fale com a Nebrys, peça à Nebrys, área da Nebrys, na Nebrys, pela Nebrys; Nebrys.',
        )
    })

    it('Texto_ArtigoMasculino_TrocaAsContracoes', () => {
        expect(textoDoFornecedor(MODELO, fornecedorDe(' Grupo Exemplo ', 'O'))).toBe(
            'O Grupo Exemplo convidou; fale com o Grupo Exemplo, peça ao Grupo Exemplo, área do Grupo Exemplo, ' +
                'no Grupo Exemplo, pelo Grupo Exemplo; Grupo Exemplo.',
        )
    })

    it('Texto_SemArtigo_SoAPreposicao', () => {
        expect(textoDoFornecedor(MODELO, fornecedorDe('Exemplo Ltda', 'nenhum'))).toBe(
            'Exemplo Ltda convidou; fale com Exemplo Ltda, peça a Exemplo Ltda, área de Exemplo Ltda, em Exemplo Ltda, ' +
                'por Exemplo Ltda; Exemplo Ltda.',
        )
    })

    it('Texto_ArtigoInvalido_ONoPadrao', () => {
        expect(fornecedorDe('Exemplo', 'os').artigo).toBe('a')
    })

    it('Texto_MarcadorDesconhecido_Falha', () => {
        expect(() => textoDoFornecedor('fale {{com fornecedor}}')).toThrow('{{com fornecedor}}')
    })

    it('FaleComOFornecedor_ComESemCanal', () => {
        expect(faleComOFornecedor(fornecedorDe('Exemplo', 'o', ' contato@exemplo.com '))).toBe(
            'fale com o Exemplo: contato@exemplo.com',
        )
        expect(faleComOFornecedor(fornecedorDe('Exemplo', 'o', '  '))).toBe('fale com o Exemplo')
    })
})
