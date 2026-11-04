import { useLayoutEffect, useRef, useState, type ChangeEvent } from 'react'
import { FieldError, Input, Text, TextField, type TextFieldProps } from 'react-aria-components'
import { Rotulo } from './Rotulo'
import { digitos, posicaoDoCursor, type Mascara } from '../format/mascaras'

interface Props extends Omit<TextFieldProps, 'children' | 'value' | 'onChange' | 'defaultValue'> {
    rotulo: string
    descricao?: string
    mascara: Mascara
    defaultValue?: string | null
    /** Recebe o valor já formatado. */
    onChange?: (valor: string) => void
    /** Controlado por fora (ex.: preenchido pelo CEP). */
    value?: string
    /**
     * Caracteres que contam para o cursor (padrão: só dígitos). O documento passa `caracteresDoDocumento`, que aceita as
     * letras do CNPJ alfanumérico; com ele, o teclado do celular deixa de ser só numérico.
     */
    caracteres?: (texto: string) => string
    /** Teclado do celular (padrão: numérico). Campo que aceita letras passa `text`. */
    teclado?: 'numeric' | 'text'
}

/**
 * Campo que formata enquanto se digita (CPF/CNPJ, telefone, CEP), com o cursor no lugar certo. Mesmo visual e regras
 * de erro do `CampoTexto`; o `name` vai no formulário com o valor formatado.
 */
export function CampoMascarado({
    rotulo,
    descricao,
    mascara,
    defaultValue,
    value,
    onChange,
    caracteres = digitos,
    teclado = 'numeric',
    className,
    ...props
}: Props) {
    const [interno, setInterno] = useState(() => mascara(defaultValue ?? ''))
    const valor = value !== undefined ? mascara(value) : interno
    const entrada = useRef<HTMLInputElement>(null)
    const cursor = useRef<number | null>(null)

    useLayoutEffect(() => {
        if (cursor.current === null || document.activeElement !== entrada.current) return
        entrada.current?.setSelectionRange(cursor.current, cursor.current)
        cursor.current = null
    })

    function mudar(evento: ChangeEvent<HTMLInputElement>) {
        const bruto = evento.target.value
        const antes = caracteres(bruto.slice(0, evento.target.selectionStart ?? bruto.length)).length
        const formatado = mascara(bruto)
        cursor.current = posicaoDoCursor(formatado, antes, (c) => caracteres(c).length === 1)
        if (value === undefined) setInterno(formatado)
        onChange?.(formatado)
    }

    return (
        <TextField
            {...props}
            value={valor}
            className={`flex flex-col gap-1 ${typeof className === 'string' ? className : ''}`}
        >
            <Rotulo texto={rotulo} obrigatorio={props.isRequired} />
            <Input
                ref={entrada}
                inputMode={teclado}
                onChange={mudar}
                className="rounded-couro border border-linha bg-superficie px-3 py-2 text-tinta tabular-nums placeholder:text-suave data-[invalid]:border-vermelho"
            />
            {descricao && (
                <Text slot="description" className="text-xs text-suave">
                    {descricao}
                </Text>
            )}
            <FieldError className="text-sm text-vermelho" />
        </TextField>
    )
}
