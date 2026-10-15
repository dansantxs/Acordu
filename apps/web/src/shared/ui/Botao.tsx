import { Button, type ButtonProps } from 'react-aria-components'

type Variante = 'primario' | 'secundario' | 'discreto'

const estilos: Record<Variante, string> = {
    primario: 'bg-acento text-botao-tinta hover:brightness-110',
    secundario: 'border border-linha bg-superficie text-tinta hover:bg-trilho',
    discreto: 'text-acento-texto underline-offset-4 hover:underline',
}

/** Botão do Acordu sobre o `Button` do React Aria (teclado, toque e `isPending` acessíveis). */
export function Botao({ variante = 'primario', className, ...props }: ButtonProps & { variante?: Variante }) {
    return (
        <Button
            {...props}
            className={`inline-flex items-center justify-center gap-2 rounded-couro px-4 py-2 text-sm font-semibold transition disabled:opacity-60 data-[pending]:cursor-wait data-[pending]:opacity-70 ${estilos[variante]} ${typeof className === 'string' ? className : ''}`}
        />
    )
}
