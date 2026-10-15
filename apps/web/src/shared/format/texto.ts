/** Para comparar sem diferenciar maiúsculas nem acentos ("Solicitações" ~ "solicitacoes"). */
export function normalizar(texto: string): string {
    return texto
        .normalize('NFD')
        .replace(/\p{Diacritic}/gu, '')
        .toLowerCase()
        .trim()
}

export function contem(texto: string, termo: string): boolean {
    return normalizar(texto).includes(normalizar(termo))
}
