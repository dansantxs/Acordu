import { Button, type ButtonProps } from 'react-aria-components'
import { useBloqueioDeGravacao } from '../sessao/gravacao'

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
    const bloqueio = useBloqueioDeGravacao(grava, descritoPor)
    return (
        <Button
            {...props}
            isDisabled={isDisabled || bloqueio.bloqueado}
            aria-describedby={bloqueio.descritoPor}
            className={`inline-flex items-center justify-center gap-2 rounded-couro px-4 py-2 text-sm font-semibold transition disabled:opacity-60 data-[pending]:cursor-wait data-[pending]:opacity-70 ${estilos[variante]} ${typeof className === 'string' ? className : ''}`}
        />
    )
}

/**
 * Botão com cara de texto (links de ação nas listas e painéis: "Editar", "Excluir", "Convidar para o portal"), com o
 * estilo de quem chama. `grava` faz o mesmo que no {@link Botao} (#191).
 */
export function BotaoTexto({
    className,
    grava = false,
    isDisabled,
    'aria-describedby': descritoPor,
    ...props
}: ButtonProps & { grava?: boolean }) {
    const bloqueio = useBloqueioDeGravacao(grava, descritoPor)
    return (
        <Button
            {...props}
            isDisabled={isDisabled || bloqueio.bloqueado}
            aria-describedby={bloqueio.descritoPor}
            className={`disabled:cursor-default disabled:opacity-60 disabled:no-underline ${typeof className === 'string' ? className : ''}`}
        />
    )
}
