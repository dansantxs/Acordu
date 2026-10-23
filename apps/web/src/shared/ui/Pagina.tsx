import { Link } from '@tanstack/react-router'
import { useEffect, type ReactNode } from 'react'

/** Cabeçalho de cada tela do app: linha de contexto, título e ações à direita. Também dá nome à aba do navegador. */
export function Pagina({
    titulo,
    contexto,
    acoes,
    ajuda,
    children,
}: {
    titulo: string
    contexto?: ReactNode
    acoes?: ReactNode
    /** Seção do manual desta tela: mostra o "?" ao lado do título. */
    ajuda?: string
    children?: ReactNode
}) {
    useEffect(() => {
        document.title = `${titulo} · Acordu`
    }, [titulo])

    return (
        <div className="flex flex-col gap-6">
            <header className="flex flex-wrap items-end gap-4">
                <div className="mr-auto flex flex-col gap-0.5">
                    {contexto && <span className="text-sm text-suave">{contexto}</span>}
                    <div className="flex items-center gap-2">
                        <h1 className="font-titulo text-3xl font-semibold tracking-tight">{titulo}</h1>
                        {ajuda && (
                            <Link
                                to="/ajuda"
                                search={{ topico: ajuda }}
                                aria-label={`Ajuda: ${titulo}`}
                                title="Como usar esta tela"
                                className="grid size-6 place-items-center rounded-full border border-linha text-xs font-semibold text-suave hover:bg-trilho hover:text-tinta"
                            >
                                ?
                            </Link>
                        )}
                    </div>
                </div>
                {acoes}
            </header>
            {children}
        </div>
    )
}
