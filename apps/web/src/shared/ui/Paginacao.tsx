import { Link } from '@tanstack/react-router'

const estilo =
    'rounded-couro border border-linha bg-superficie px-3 py-1.5 text-sm hover:bg-trilho aria-disabled:pointer-events-none aria-disabled:opacity-50'

/** "Anterior / Próxima" com a página no endereço (`?pagina=`), para voltar ao mesmo ponto. */
export function Paginacao({ pagina, tamanhoPagina, total }: { pagina: number; tamanhoPagina: number; total: number }) {
    const paginas = Math.max(1, Math.ceil(total / tamanhoPagina))
    if (paginas <= 1) return null
    return (
        <nav aria-label="Páginas" className="flex items-center justify-between gap-3 text-sm text-suave">
            <span>
                Página {pagina} de {paginas} · {total} no total
            </span>
            <span className="flex gap-2">
                <Link
                    to="."
                    search={(atual: Record<string, unknown>) => ({
                        ...atual,
                        pagina: pagina - 1 > 1 ? pagina - 1 : undefined,
                    })}
                    aria-disabled={pagina <= 1}
                    className={estilo}
                >
                    Anterior
                </Link>
                <Link
                    to="."
                    search={(atual: Record<string, unknown>) => ({ ...atual, pagina: pagina + 1 })}
                    aria-disabled={pagina >= paginas}
                    className={estilo}
                >
                    Próxima
                </Link>
            </span>
        </nav>
    )
}
