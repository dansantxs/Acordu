import privacidadeBruto from './textos/privacidade.md?raw'
import termosBruto from './textos/termos.md?raw'

/**
 * Textos legais (F14f, D10 do F14): Markdown versionado no SPA, com `versao` e `vigencia` no cabeçalho. As páginas
 * `/termos` e `/privacidade` são públicas e não chamam a API (abrem sem acordar o Cloud Run nem o Neon, ADR 0010).
 *
 * O formato aceito é um subconjunto pequeno do Markdown, o que os textos usam: `#`, `##` e `###`, parágrafos, listas com
 * `-`, `**negrito**` e `[texto](endereço)`. Nada vira HTML cru: tudo sai como elementos do React.
 */
export type TipoTextoLegal = 'termos' | 'privacidade'

export type Bloco =
    | { tipo: 'titulo'; nivel: 1 | 2 | 3; texto: string }
    | { tipo: 'paragrafo'; texto: string }
    | { tipo: 'lista'; itens: string[] }

export interface TextoLegal {
    versao: string
    /** Data civil (`yyyy-MM-dd`) em que a versão passa a valer. */
    vigencia: string
    /** O corpo, sem o cabeçalho, com as quebras de linha normalizadas (`\n`): é o que a impressão confere. */
    corpo: string
    blocos: Bloco[]
}

const CABECALHO = /^---\n([\s\S]*?)\n---\n/

/** Lê o cabeçalho e os blocos de um texto legal. Cabeçalho sem `versao` ou `vigencia` é defeito do arquivo. */
export function lerTextoLegal(bruto: string): TextoLegal {
    const texto = bruto.replace(/\r\n?/g, '\n')
    const cabecalho = CABECALHO.exec(texto)
    if (!cabecalho) throw new Error('Texto legal sem cabeçalho (--- versao, vigencia ---).')
    const campos = new Map<string, string>()
    for (const linha of (cabecalho[1] ?? '').split('\n')) {
        const separador = linha.indexOf(':')
        if (separador < 0) continue
        const valor = linha
            .slice(separador + 1)
            .trim()
            .replace(/^['"]|['"]$/g, '')
        campos.set(linha.slice(0, separador).trim(), valor)
    }
    const versao = campos.get('versao')
    const vigencia = campos.get('vigencia')
    if (!versao || !vigencia || !/^\d{4}-\d{2}-\d{2}$/.test(vigencia)) {
        throw new Error('Texto legal sem versao ou com vigencia fora do formato yyyy-MM-dd.')
    }
    const corpo = texto.slice(cabecalho[0].length).trim()
    return { versao, vigencia, corpo, blocos: lerBlocos(corpo) }
}

function lerBlocos(corpo: string): Bloco[] {
    const blocos: Bloco[] = []
    const paragrafo: string[] = []
    const lista: string[] = []
    const fecharParagrafo = () => {
        if (paragrafo.length > 0) blocos.push({ tipo: 'paragrafo', texto: paragrafo.join(' ') })
        paragrafo.length = 0
    }
    const fecharLista = () => {
        if (lista.length > 0) blocos.push({ tipo: 'lista', itens: [...lista] })
        lista.length = 0
    }
    for (const linhaCrua of corpo.split('\n')) {
        const linha = linhaCrua.trim()
        const titulo = /^(#{1,3})\s+(.*)$/.exec(linha)
        if (linha === '') {
            fecharParagrafo()
            fecharLista()
        } else if (titulo) {
            fecharParagrafo()
            fecharLista()
            blocos.push({ tipo: 'titulo', nivel: titulo[1]!.length as 1 | 2 | 3, texto: titulo[2]! })
        } else if (linha.startsWith('- ')) {
            fecharParagrafo()
            lista.push(linha.slice(2).trim())
        } else if (lista.length > 0 && linhaCrua.startsWith(' ')) {
            // Continuação de um item de lista quebrado em mais de uma linha.
            lista[lista.length - 1] += ` ${linha}`
        } else {
            fecharLista()
            paragrafo.push(linha)
        }
    }
    fecharParagrafo()
    fecharLista()
    return blocos
}

export const TEXTOS: Record<TipoTextoLegal, TextoLegal> = {
    termos: lerTextoLegal(termosBruto),
    privacidade: lerTextoLegal(privacidadeBruto),
}

/** `2026-11-16` → `16/11/2026`. */
export const dataDaVigencia = (data: string) => data.split('-').reverse().join('/')
