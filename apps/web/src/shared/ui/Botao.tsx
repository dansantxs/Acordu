import { Button, type ButtonProps } from 'react-aria-components'
import { useGravacao } from '../sessao/gravacao'

type Variante = 'primario' | 'secundario' | 'discreto'

const estilos: Record<Variante, string> = {
    primario: 'bg-acento text-botao-tinta hover:brightness-110',
    secundario: 'border border-linha bg-superficie text-tinta hover:bg-trilho',
    discreto: 'text-acento-texto underline-offset-4 hover:underline',
}

/**
 * Botão do Acordu sobre o `Button` do React Aria (teclado, toque e `isPending` acessíveis). `grava`: a ação grava dado
 * da empresa; com a empresa só leitura (ou o envio indisponível no portal) o botão fica indisponível, ligado à
 * explicação da casca (`podeGravar`, convenções §4; F14-26, F14-28).
 */
export function Botao({
    variante = 'primario',
    className,
    grava = false,
    isDisabled,
    'aria-describedby': descritoPor,
    ...props
}: ButtonProps & { variante?: Variante; grava?: boolean }) {
    const gravacao = useGravacao()
    const bloqueado = grava && !gravacao.podeGravar
    return (
        <Button
            {...props}
            isDisabled={isDisabled || bloqueado}
            aria-describedby={
                bloqueado ? [descritoPor, gravacao.idMotivo].filter(Boolean).join(' ') || undefined : descritoPor
            }
            className={`inline-flex items-center justify-center gap-2 rounded-couro px-4 py-2 text-sm font-semibold transition disabled:opacity-60 data-[pending]:cursor-wait data-[pending]:opacity-70 ${estilos[variante]} ${typeof className === 'string' ? className : ''}`}
        />
    )
}
