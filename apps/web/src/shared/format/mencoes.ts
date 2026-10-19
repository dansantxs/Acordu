/**
 * Menções (contrato §7, 14b): o texto gravado cita a pessoa como `@{usuarioId}`. Na tela vira `@Nome`; na digitação a
 * pessoa escolhe da lista da equipe e o nome é trocado pelo token só no envio.
 */
const TOKEN = /@\{([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\}/gi

export type Parte = { tipo: 'texto'; texto: string } | { tipo: 'mencao'; id: string; nome: string | null }

/** Separa o texto gravado em trechos e menções; `nome` nulo quando a pessoa não está mais na equipe. */
export function partesDoTexto(texto: string, nomes: ReadonlyMap<string, string>): Parte[] {
    const partes: Parte[] = []
    let inicio = 0
    for (const achado of texto.matchAll(TOKEN)) {
        if (achado.index > inicio) partes.push({ tipo: 'texto', texto: texto.slice(inicio, achado.index) })
        const id = achado[1]!.toLowerCase()
        partes.push({ tipo: 'mencao', id, nome: nomes.get(id) ?? null })
        inicio = achado.index + achado[0].length
    }
    if (inicio < texto.length) partes.push({ tipo: 'texto', texto: texto.slice(inicio) })
    return partes
}

export interface Mencionado {
    id: string
    nome: string
}

/** No envio: cada `@Nome` escolhido da lista vira `@{id}` (nomes mais longos primeiro, para "Ana" não pegar "Ana Paula"). */
export function textoComTokens(texto: string, mencionados: readonly Mencionado[]): string {
    return [...mencionados]
        .sort((a, b) => b.nome.length - a.nome.length)
        .reduce((atual, m) => atual.replaceAll(`@${m.nome}`, `@{${m.id}}`), texto)
}

/** O que está sendo digitado depois de um `@` no começo do texto ou após espaço (`null` fora de uma menção). */
export function mencaoEmDigitacao(antesDoCursor: string): string | null {
    const achado = /(?:^|\s)@([\p{L}\p{N}._-]{0,30})$/u.exec(antesDoCursor)
    return achado ? achado[1]! : null
}
