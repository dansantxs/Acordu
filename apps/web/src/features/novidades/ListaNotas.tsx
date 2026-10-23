import { dataDaNota, type NotaVersao } from './notas'

/** As notas de um público, mais recente primeiro (Novidades da equipe e guia do portal). */
export function ListaNotas({ notas, nivel = 2 }: { notas: NotaVersao[]; nivel?: 2 | 3 }) {
    const Titulo = nivel === 2 ? 'h2' : 'h3'
    return (
        <ol className="flex flex-col gap-4">
            {notas.map((n) => (
                <li
                    key={`${n.data}-${n.titulo}`}
                    className="flex flex-col gap-2 rounded-couro border border-linha bg-superficie p-4"
                >
                    <span className="text-xs text-suave">
                        {n.versao ? `Versão ${n.versao} · ` : ''}
                        <time dateTime={n.data}>{dataDaNota(n.data)}</time>
                    </span>
                    <Titulo className="text-lg font-semibold">{n.titulo}</Titulo>
                    <ul className="flex list-disc flex-col gap-1 pl-5 text-sm">
                        {n.itens.map((i) => (
                            <li key={i}>{i}</li>
                        ))}
                    </ul>
                </li>
            ))}
        </ol>
    )
}

/** Ponto discreto ao lado de um item de menu quando há novidade não vista. */
export function PontoNovidade() {
    return (
        <span className="flex items-center">
            <span aria-hidden="true" className="size-2 rounded-full bg-dourado" />
            <span className="sr-only">, há novidades</span>
        </span>
    )
}
