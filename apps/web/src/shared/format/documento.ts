import { caracteresDoDocumento } from './mascaras'

/** Valor de cada caractere no cálculo do dígito: código ASCII menos 48 (dígitos 0–9; letras A–Z de 17 a 42). */
const valor = (c: string) => c.charCodeAt(0) - 48

/** Dígito verificador módulo 11 sobre os caracteres, com os pesos dados (da esquerda para a direita). */
function digito(caracteres: string, pesos: number[]): number {
    const soma = pesos.reduce((total, peso, i) => total + valor(caracteres[i]!) * peso, 0)
    const resto = soma % 11
    return resto < 2 ? 0 : 11 - resto
}

function cpfValido(d: string): boolean {
    if (!/^\d{11}$/.test(d) || /^(\d)\1+$/.test(d)) return false
    const p1 = [10, 9, 8, 7, 6, 5, 4, 3, 2]
    const p2 = [11, ...p1]
    return digito(d, p1) === valor(d[9]!) && digito(d, p2) === valor(d[10]!)
}

function cnpjValido(d: string): boolean {
    if (!/^[0-9A-Z]{12}\d{2}$/.test(d) || /^(.)\1+$/.test(d)) return false
    const p1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]
    const p2 = [6, ...p1]
    return digito(d, p1) === valor(d[12]!) && digito(d, p2) === valor(d[13]!)
}

/** CPF ou CNPJ (numérico ou alfanumérico) com os dígitos verificadores certos; o CPF nunca tem letra. */
export function documentoValido(texto: string): boolean {
    const d = caracteresDoDocumento(texto)
    return d.length === 11 ? cpfValido(d) : d.length === 14 ? cnpjValido(d) : false
}

/** CNPJ completo e válido (o que pode ser consultado na Receita); CPF nunca. */
export function cnpjCompleto(texto: string): string | null {
    const d = caracteresDoDocumento(texto)
    return d.length === 14 && cnpjValido(d) ? d : null
}

/**
 * Validação do campo CPF ou CNPJ no formulário (spec do F10, decisão 5): vazio passa (o documento é opcional); a API
 * confere de novo ao salvar.
 */
export function validarDocumento(texto: string): string | null {
    return texto.trim() === '' || documentoValido(texto) ? null : 'CPF ou CNPJ inválido.'
}
