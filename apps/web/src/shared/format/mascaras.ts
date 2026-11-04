/**
 * Máscaras dos campos brasileiros (pedido de 16/10): formatam enquanto se digita e para exibir. A API recebe o texto
 * formatado e guarda só os dígitos onde precisa (documento, CEP); o telefone fica como digitado.
 */
export const digitos = (texto: string): string => texto.replace(/\D/g, '')

/**
 * Caracteres de um CPF ou CNPJ, em maiúsculas: dígitos e, só nas 12 primeiras posições, letras (CNPJ alfanumérico,
 * IN RFB 2.229/2024). Letra depois disso ou além de 14 caracteres é descartada.
 */
export function caracteresDoDocumento(texto: string): string {
    // Texto colado de nota ou e-mail ("CNPJ: 11.222…", "CPF nº 529…"): o rótulo não pode virar letras do CNPJ.
    const semRotulo = texto.replace(/^[^:]*:/, '').replace(/^\s*(CNPJ|CPF)\b[\s.]*(N[º°O.]*\s*)?/i, '')
    let saida = ''
    for (const c of semRotulo.toUpperCase()) {
        if (saida.length >= 14) break
        if (/\d/.test(c) || (/[A-Z]/.test(c) && saida.length < 12)) saida += c
    }
    return saida
}

/** Aplica um molde (`#` = um caractere) até onde houver caracteres, sem deixar separador pendurado no fim. */
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

/**
 * CPF (`000.000.000-00`) até 11 dígitos; CNPJ (`00.000.000/0000-00`) a partir do 12º ou com qualquer letra (o CPF só
 * tem dígitos; o CNPJ alfanumérico fica `12.ABC.345/01DE-35`).
 */
export function formatarDocumento(texto: string): string {
    const caracteres = caracteresDoDocumento(texto)
    const cpf = caracteres.length <= 11 && !/[A-Z]/.test(caracteres)
    return cpf ? moldar(caracteres, '###.###.###-##') : moldar(caracteres, '##.###.###/####-##')
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
 * Onde deixar o cursor depois de formatar: logo após o mesmo número de caracteres significativos (dígitos; no
 * documento, também letras) que havia antes dele. Assim, apagar ou digitar no meio não joga o cursor para o fim.
 */
export function posicaoDoCursor(
    formatado: string,
    antesDoCursor: number,
    significativo: (caractere: string) => boolean = (c) => /\d/.test(c),
): number {
    if (antesDoCursor <= 0) return 0
    let vistos = 0
    for (let i = 0; i < formatado.length; i++) {
        if (significativo(formatado[i]!)) vistos++
        if (vistos === antesDoCursor) return i + 1
    }
    return formatado.length
}
