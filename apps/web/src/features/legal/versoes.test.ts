import tiposDaApi from '@/shared/api/interno.d.ts?raw'
import termosBruto from './textos/termos.md?raw'
import { describe, expect, it } from 'vitest'
import { lerTextoLegal, TEXTOS, type TipoTextoLegal } from './textosLegais'
import { VERSOES_TEXTOS_LEGAIS } from './versoes'

/** FNV-1a de 32 bits, em hex: basta para notar que o corpo mudou (não é segurança). */
function impressao(texto: string): string {
    let h = 0x811c9dc5
    for (const unidade of new TextEncoder().encode(texto)) {
        h ^= unidade
        h = Math.imul(h, 0x01000193) >>> 0
    }
    return h.toString(16).padStart(8, '0')
}

const TIPOS: TipoTextoLegal[] = ['termos', 'privacidade']

/**
 * Valores do `enum` de um campo de um esquema, lidos dos tipos gerados do OpenAPI por `pnpm api:tipos`
 * (`campo?: '1' | null`).
 */
function enumDoCampo(esquema: string, campo: string): string[] {
    const texto = tiposDaApi.replace(/\r\n/g, '\n')
    const inicio = texto.indexOf(`\n        ${esquema}: {\n`)
    if (inicio < 0) return []
    const bloco = texto.slice(inicio + 1, texto.indexOf('\n        }', inicio + 1))
    const linha = bloco.split('\n').find((l) => l.trim().startsWith(`${campo}?:`) || l.trim().startsWith(`${campo}:`))
    return [...(linha ?? '').matchAll(/'([^']*)'/g)].map((m) => m[1] ?? '')
}

// D10 do F14 (convenções §4): mudar um texto legal é mudar a versão no arquivo e na constante, no mesmo PR.
describe('textos legais: versão (D10)', () => {
    it.each(TIPOS)('TextoLegal_VersaoEVigenciaDoArquivo_IguaisAConstante (%s)', (tipo) => {
        const { versao, vigencia } = VERSOES_TEXTOS_LEGAIS[tipo]
        expect({ versao: TEXTOS[tipo].versao, vigencia: TEXTOS[tipo].vigencia }).toEqual({ versao, vigencia })
    })

    it.each(TIPOS)('TextoLegal_CorpoMudou_ExigeVersaoNovaEImpressaoNova (%s)', (tipo) => {
        const calculada = impressao(TEXTOS[tipo].corpo)
        expect(
            calculada,
            `O texto "${tipo}" mudou: publique versão e vigência novas no arquivo e em versoes.ts, com a impressão "${calculada}".`,
        ).toBe(VERSOES_TEXTOS_LEGAIS[tipo].impressao)
    })

    // F14h: a API confere o aceite contra a constante TextosLegais, publicada no OpenAPI como o enum do campo.
    it.each([
        ['termos', 'versaoTermos'],
        ['privacidade', 'versaoPrivacidade'],
    ] as const)('TextoLegal_VersaoDaConstante_IgualAoEnumDaApi (%s)', (tipo, campo) => {
        for (const corpo of ['RegistroRequest', 'AceiteConviteEquipeRequest']) {
            expect(enumDoCampo(corpo, campo), corpo).toEqual([VERSOES_TEXTOS_LEGAIS[tipo].versao])
        }
    })

    it('TextoLegal_QuebraDeLinhaDoWindows_MesmaImpressao', () => {
        const comCrLf = termosBruto.replace(/\r?\n/g, '\r\n')
        expect(impressao(lerTextoLegal(comCrLf).corpo)).toBe(impressao(TEXTOS.termos.corpo))
    })
})

describe('textos legais: leitura', () => {
    it('LerTextoLegal_CabecalhoEBlocos_LeVersaoVigenciaTitulosListasEParagrafos', () => {
        const texto = lerTextoLegal(
            "---\nversao: '2'\nvigencia: '2027-01-02'\n---\n\n# Título\n\nUm parágrafo\nem duas linhas.\n\n## Seção\n\n- um item\n- outro **forte**\n",
        )
        expect(texto.versao).toBe('2')
        expect(texto.vigencia).toBe('2027-01-02')
        expect(texto.blocos).toEqual([
            { tipo: 'titulo', nivel: 1, texto: 'Título' },
            { tipo: 'paragrafo', texto: 'Um parágrafo em duas linhas.' },
            { tipo: 'titulo', nivel: 2, texto: 'Seção' },
            { tipo: 'lista', itens: ['um item', 'outro **forte**'] },
        ])
    })

    it('LerTextoLegal_SemVersaoOuVigenciaInvalida_Falha', () => {
        expect(() => lerTextoLegal('# Sem cabeçalho')).toThrow()
        expect(() => lerTextoLegal("---\nvigencia: '2027-01-02'\n---\ntexto")).toThrow()
        expect(() => lerTextoLegal("---\nversao: '1'\nvigencia: 02/01/2027\n---\ntexto")).toThrow()
    })

    it.each(TIPOS)('TextoLegal_Publicado_TemTituloECanalDaNebrys (%s)', (tipo) => {
        expect(TEXTOS[tipo].blocos[0]).toMatchObject({ tipo: 'titulo', nivel: 1 })
        expect(TEXTOS[tipo].corpo).toContain('contato@acordu.app.br')
    })
})
