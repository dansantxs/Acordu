import { useEffect, type ReactNode } from 'react'

/** Cabeçalho de cada tela do app: linha de contexto, título e ações à direita. Também dá nome à aba do navegador. */
export function Pagina({
    titulo,
    contexto,
    acoes,
    children,
}: {
    titulo: string
    contexto?: ReactNode
    acoes?: ReactNode
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
                    <h1 className="font-titulo text-3xl font-semibold tracking-tight">{titulo}</h1>
                </div>
                {acoes}
            </header>
            {children}
        </div>
    )
}
