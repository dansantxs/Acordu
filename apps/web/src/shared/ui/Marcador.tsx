import type { ReactNode } from 'react'
import { Checkbox } from 'react-aria-components'

/** Caixa de marcar do Acordu sobre o `Checkbox` do React Aria (teclado e leitor de tela). */
export function Marcador({
    valor,
    aoMudar,
    isDisabled,
    descritoPor,
    children,
}: {
    valor: boolean
    aoMudar: (valor: boolean) => void
    isDisabled?: boolean
    /** Id do texto que explica o estado (por exemplo, por que está desabilitado). */
    descritoPor?: string
    children: ReactNode
}) {
    return (
        <Checkbox
            isSelected={valor}
            onChange={aoMudar}
            isDisabled={isDisabled}
            aria-describedby={descritoPor}
            className="group flex items-center gap-2 text-sm data-[disabled]:opacity-60"
        >
            <span
                aria-hidden="true"
                className="grid size-4 shrink-0 place-items-center rounded border border-linha bg-superficie group-data-[selected]:border-acento group-data-[selected]:bg-acento group-data-[focus-visible]:ring-2 group-data-[focus-visible]:ring-acento"
            >
                <svg
                    viewBox="0 0 12 12"
                    className="size-3 text-botao-tinta opacity-0 group-data-[selected]:opacity-100"
                >
                    <path d="M2.5 6.5 5 9l4.5-6" fill="none" stroke="currentColor" strokeWidth="2" />
                </svg>
            </span>
            {children}
        </Checkbox>
    )
}
