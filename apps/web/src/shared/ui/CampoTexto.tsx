import type { Ref } from 'react'
import { FieldError, Input, Text, TextArea, TextField, type TextFieldProps } from 'react-aria-components'
import { Rotulo } from './Rotulo'

interface Props extends Omit<TextFieldProps, 'children' | 'type'> {
    /** Senha não: use `CampoSenha` (convenções §4). */
    type?: Exclude<TextFieldProps['type'], 'password'>
    rotulo: string
    descricao?: string
    /** Várias linhas (observações, mensagens). */
    multilinha?: boolean
    /** Para levar o foco ao campo (ex.: o número, depois que o CEP preenche o endereço). */
    refEntrada?: Ref<HTMLInputElement>
}

export const estiloEntrada =
    'rounded-couro border border-linha bg-superficie px-3 py-2 text-tinta placeholder:text-suave data-[invalid]:border-vermelho'

/**
 * Campo de texto com rótulo, descrição e erro. O `name` é o do corpo da API (camelCase): dentro de um `Form` com
 * `validationErrors`, os erros de campo do Problem Details aparecem sozinhos no campo certo.
 */
export function CampoTexto({ rotulo, descricao, multilinha, refEntrada, className, ...props }: Props) {
    return (
        <TextField {...props} className={`flex flex-col gap-1 ${typeof className === 'string' ? className : ''}`}>
            <Rotulo texto={rotulo} obrigatorio={props.isRequired} />
            {multilinha ? (
                <TextArea rows={4} className={`${estiloEntrada} resize-y`} />
            ) : (
                <Input ref={refEntrada} className={estiloEntrada} />
            )}
            {descricao && (
                <Text slot="description" className="text-xs text-suave">
                    {descricao}
                </Text>
            )}
            <FieldError className="text-sm text-vermelho" />
        </TextField>
    )
}
