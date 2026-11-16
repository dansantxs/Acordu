import type { ReactNode } from 'react'
import type { Bloco } from './textosLegais'

const estiloLink = 'text-acento-texto underline underline-offset-4'

/** Negrito (`**x**`) e links (`[texto](endereço)`), sem HTML cru. */
function trechos(texto: string): ReactNode[] {
    const partes: ReactNode[] = []
    const marca = /\*\*(.+?)\*\*|\[([^\]]+)\]\(([^)\s]+)\)/g
    let inicio = 0
    for (const achado of texto.matchAll(marca)) {
        if (achado.index > inicio) partes.push(texto.slice(inicio, achado.index))
        const [, negrito, rotulo, endereco] = achado
        partes.push(
            negrito !== undefined ? (
                <strong key={achado.index}>{negrito}</strong>
            ) : (
                <a key={achado.index} href={endereco} className={estiloLink}>
                    {rotulo}
                </a>
            ),
        )
        inicio = achado.index + achado[0].length
    }
    if (inicio < texto.length) partes.push(texto.slice(inicio))
    return partes
}

/** Os blocos de um texto legal (`lerTextoLegal`). O título (`#`) fica com a página, que mostra a versão logo abaixo. */
export function TextoMarcado({ blocos }: { blocos: Bloco[] }) {
    return (
        <div className="flex flex-col gap-4 leading-relaxed">
            {blocos.map((bloco, i) => {
                if (bloco.tipo === 'titulo') {
                    if (bloco.nivel === 1) return null
                    return bloco.nivel === 2 ? (
                        <h2 key={i} className="mt-4 font-titulo text-xl font-semibold">
                            {trechos(bloco.texto)}
                        </h2>
                    ) : (
                        <h3 key={i} className="mt-2 font-semibold">
                            {trechos(bloco.texto)}
                        </h3>
                    )
                }
                if (bloco.tipo === 'lista') {
                    return (
                        <ul key={i} className="flex list-disc flex-col gap-2 pl-6">
                            {bloco.itens.map((item, j) => (
                                <li key={j}>{trechos(item)}</li>
                            ))}
                        </ul>
                    )
                }
                return <p key={i}>{trechos(bloco.texto)}</p>
            })}
        </div>
    )
}
