import { ARTIGO_FORNECEDOR, CONTATO_FORNECEDOR, NOME_FORNECEDOR } from './ambiente'

/** O padrão sem `VITE_NOME_FORNECEDOR`, o mesmo da API (`OpcoesFornecedor.NomePadrao`). */
export const NOME_PADRAO_FORNECEDOR = 'Nebrys'

/**
 * O fornecedor nos textos (ADR 0017), como `OpcoesFornecedor` na API: os textos usam marcadores escritos no feminino,
 * trocados pelo artigo configurado. `{{fornecedor}}` é só o nome; `{{a fornecedor}}`, `{{A fornecedor}}`,
 * `{{da fornecedor}}`, `{{à fornecedor}}`, `{{na fornecedor}}` e `{{pela fornecedor}}` (e as formas com maiúscula) levam o
 * artigo ou a preposição: "a Nebrys", "o Grupo Exemplo", "Exemplo Ltda".
 */
export type ArtigoFornecedor = 'a' | 'o' | 'nenhum'

export interface Fornecedor {
    nome: string
    artigo: ArtigoFornecedor
    contato?: string
}

const FORMAS: Record<string, Record<ArtigoFornecedor, string>> = {
    a: { a: 'a ', o: 'o ', nenhum: '' },
    da: { a: 'da ', o: 'do ', nenhum: 'de ' },
    à: { a: 'à ', o: 'ao ', nenhum: 'a ' },
    na: { a: 'na ', o: 'no ', nenhum: 'em ' },
    pela: { a: 'pela ', o: 'pelo ', nenhum: 'por ' },
}

/** Artigo fora de `a`, `o` e `nenhum` fica no padrão: o `deploy.yml` já barra o valor inválido antes de publicar. */
export function fornecedorDe(nome?: string, artigo?: string, contato?: string): Fornecedor {
    const normalizado = artigo?.trim().toLowerCase()
    return {
        nome: nome?.trim() || NOME_PADRAO_FORNECEDOR,
        artigo: normalizado === 'o' || normalizado === 'nenhum' ? normalizado : 'a',
        contato: contato?.trim() || undefined,
    }
}

export const FORNECEDOR = fornecedorDe(NOME_FORNECEDOR, ARTIGO_FORNECEDOR, CONTATO_FORNECEDOR)

/** Troca os marcadores do fornecedor no modelo. Marcador desconhecido é defeito do texto. */
export function textoDoFornecedor(modelo: string, fornecedor: Fornecedor = FORNECEDOR): string {
    return modelo.replace(/\{\{(?:([^\s{}]+) )?fornecedor\}\}/g, (marcador, forma: string | undefined) => {
        if (!forma) return fornecedor.nome
        const formas = FORMAS[forma.toLowerCase()]
        if (!formas) throw new Error(`Marcador do fornecedor desconhecido: "${marcador}".`)
        let prefixo = formas[fornecedor.artigo]
        if (forma[0] !== forma[0]!.toLowerCase() && prefixo) prefixo = prefixo[0]!.toUpperCase() + prefixo.slice(1)
        return prefixo + fornecedor.nome
    })
}

/** "fale com a Nebrys: {canal}", ou só "fale com a Nebrys" sem o canal no build. */
export function faleComOFornecedor(fornecedor: Fornecedor = FORNECEDOR): string {
    const fale = textoDoFornecedor('fale com {{a fornecedor}}', fornecedor)
    return fornecedor.contato ? `${fale}: ${fornecedor.contato}` : fale
}
