/**
 * Máscaras dos campos brasileiros (pedido de 16/10): formatam enquanto se digita e para exibir. A API recebe o texto
 * formatado e guarda só os dígitos onde precisa (documento, CEP); o telefone fica como digitado.
 */
export const digitos = (texto: string): string => texto.replace(/\D/g, '')

/** Aplica um molde (`#` = dígito) até onde houver dígitos, sem deixar separador pendurado no fim. */
function moldar(numeros: string, molde: string): string {
    let saida = ''
    let i = 0
    for (const caractere of molde) {
        if (i >= numeros.length) break
        if (caractere === '#') saida += numeros[i++]
        else saida += caractere
    }
    return saida
}

/** CPF (`000.000.000-00`) até 11 dígitos; CNPJ (`00.000.000/0000-00`) a partir do 12º. */
export function formatarDocumento(texto: string): string {
    const numeros = digitos(texto).slice(0, 14)
    return numeros.length <= 11 ? moldar(numeros, '###.###.###-##') : moldar(numeros, '##.###.###/####-##')
}

/**
 * Telefone brasileiro: `(18) 3333-0000` (fixo) ou `(18) 99999-0000` (celular). Começando com `+`, é internacional e
 * fica como digitado.
 */
export function formatarTelefone(texto: string): string {
    if (texto.trimStart().startsWith('+')) return texto
    const numeros = digitos(texto).slice(0, 11)
    if (numeros.length <= 2) return numeros.length ? `(${numeros}` : ''
    return numeros.length <= 10 ? moldar(numeros, '(##) ####-####') : moldar(numeros, '(##) #####-####')
}

export function formatarCep(texto: string): string {
    return moldar(digitos(texto).slice(0, 8), '#####-###')
}

export type Mascara = (texto: string) => string

/**
 * Onde deixar o cursor depois de formatar: logo após o mesmo número de dígitos que havia antes dele. Assim, apagar ou
 * digitar no meio não joga o cursor para o fim.
 */
export function posicaoDoCursor(formatado: string, digitosAntesDoCursor: number): number {
    if (digitosAntesDoCursor <= 0) return 0
    let vistos = 0
    for (let i = 0; i < formatado.length; i++) {
        if (/\d/.test(formatado[i]!)) vistos++
        if (vistos === digitosAntesDoCursor) return i + 1
    }
    return formatado.length
}
