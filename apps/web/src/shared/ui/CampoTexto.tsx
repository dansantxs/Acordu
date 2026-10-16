import { FieldError, Input, Label, Text, TextArea, TextField, type TextFieldProps } from 'react-aria-components'

interface Props extends Omit<TextFieldProps, 'children'> {
    rotulo: string
    descricao?: string
    /** Várias linhas (observações, mensagens). */
    multilinha?: boolean
}

const estiloEntrada =
    'rounded-couro border border-linha bg-superficie px-3 py-2 text-tinta placeholder:text-suave data-[invalid]:border-vermelho'

/**
 * Campo de texto com rótulo, descrição e erro. O `name` é o do corpo da API (camelCase): dentro de um `Form` com
 * `validationErrors`, os erros de campo do Problem Details aparecem sozinhos no campo certo.
 */
export function CampoTexto({ rotulo, descricao, multilinha, className, ...props }: Props) {
    return (
        <TextField {...props} className={`flex flex-col gap-1 ${typeof className === 'string' ? className : ''}`}>
            <Label className="text-sm font-semibold text-tinta">{rotulo}</Label>
            {multilinha ? (
                <TextArea rows={4} className={`${estiloEntrada} resize-y`} />
            ) : (
                <Input className={estiloEntrada} />
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
