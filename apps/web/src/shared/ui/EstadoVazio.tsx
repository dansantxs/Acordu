import type { ReactNode } from 'react'

/** Lugar vazio de uma lista ou tela: uma página de agenda ainda sem anotação. */
export function EstadoVazio({ titulo, children, acao }: { titulo: string; children?: ReactNode; acao?: ReactNode }) {
    return (
        <div className="flex flex-col items-start gap-3 rounded-couro border border-dashed border-linha bg-superficie/60 px-6 py-10">
            <p className="font-titulo text-xl font-semibold">{titulo}</p>
            {children && <div className="max-w-prose text-sm text-suave">{children}</div>}
            {acao}
        </div>
    )
}
