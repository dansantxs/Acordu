/**
 * Endereço do atalho "Falar no WhatsApp" (#208): `https://wa.me/<E.164 só dígitos>`, aberto numa aba nova, sem
 * integração (o Acordu não guarda nem envia nada). Nulo quando o telefone não serve, e aí o atalho não aparece.
 *
 * - com `+`: já é internacional e fica como está (de 8 a 15 dígitos, o limite do E.164); com +55, passa pela regra
 *   brasileira abaixo;
 * - sem `+`: zeros à esquerda saem (`014…`, `0055…`); 10 ou 11 dígitos são DDD e número brasileiros e ganham o 55;
 *   12 ou 13 começando por 55 já trazem o DDI;
 * - brasileiro: o DDD tem de existir (`DDDS`) e, com 11 dígitos, o número começa por 9 (celular); com 10 dígitos é
 *   fixo, que também pode ter WhatsApp (Business). Assim 0800, 0300 e número estrangeiro sem `+` não viram link;
 * - vazio, com letras (ramal escrito, texto colado) ou com dígitos de menos ou de mais: nulo.
 */
export function linkWhatsApp(telefone: string | null | undefined): string | null {
    const texto = telefone?.trim() ?? ''
    if (!texto || /\p{L}/u.test(texto)) return null
    const numeros = texto.replace(/\D/g, '')
    const e164 = texto.startsWith('+') ? internacional(numeros) : nacional(numeros.replace(/^0+/, ''))
    return e164 ? `https://wa.me/${e164}` : null
}

function internacional(numeros: string): string | null {
    if (numeros.startsWith('55')) return comDdiBrasil(numeros)
    return numeros.length >= 8 && numeros.length <= 15 ? numeros : null
}

function nacional(numeros: string): string | null {
    if (numeros.length === 10 || numeros.length === 11) return brasileiro(numeros) ? `55${numeros}` : null
    return numeros.startsWith('55') ? comDdiBrasil(numeros) : null
}

const comDdiBrasil = (numeros: string): string | null =>
    (numeros.length === 12 || numeros.length === 13) && brasileiro(numeros.slice(2)) ? numeros : null

/** DDD e número, sem o 55: DDD que existe e, com 11 dígitos, celular (terceiro dígito 9). */
function brasileiro(numeros: string): boolean {
    if (!DDDS.has(numeros.slice(0, 2))) return false
    return numeros.length === 10 || (numeros.length === 11 && numeros[2] === '9')
}

/** DDDs brasileiros em uso (Anatel). Lista fechada: 20, 23, 25, 80 etc. não existem. */
const DDDS = new Set(
    (
        [
            [11, 19],
            [21, 22],
            [24, 24],
            [27, 28],
            [31, 35],
            [37, 38],
            [41, 49],
            [51, 51],
            [53, 55],
            [61, 69],
            [71, 71],
            [73, 75],
            [77, 77],
            [79, 79],
            [81, 89],
            [91, 99],
        ] as const satisfies readonly (readonly [number, number])[]
    ).flatMap(([de, ate]) => Array.from({ length: ate - de + 1 }, (_, i) => String(de + i))),
)
