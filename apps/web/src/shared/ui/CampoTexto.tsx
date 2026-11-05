import { useState, type Ref } from 'react'
import { Button, FieldError, Input, Text, TextArea, TextField, type TextFieldProps } from 'react-aria-components'
import { Icone } from './Icone'
import { Rotulo } from './Rotulo'

interface Props extends Omit<TextFieldProps, 'children'> {
    rotulo: string
    descricao?: string
    /** Várias linhas (observações, mensagens). */
    multilinha?: boolean
    /** Para levar o foco ao campo (ex.: o número, depois que o CEP preenche o endereço). */
    refEntrada?: Ref<HTMLInputElement>
}

const estiloEntrada =
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

/**
 * Campo de senha com o botão de mostrar e ocultar (o "olhinho"). Todo campo de senha usa este (convenções §4): o botão
 * fica na ordem do Tab, diz o que faz pelo `aria-label` e pelo `aria-pressed`, e esconde o botão nativo do Edge para
 * não aparecerem dois.
 */
export function CampoSenha({
    rotulo,
    descricao,
    className,
    ...props
}: Omit<Props, 'multilinha' | 'refEntrada' | 'type'>) {
    const [visivel, setVisivel] = useState(false)
    return (
        <TextField
            {...props}
            type={visivel ? 'text' : 'password'}
            className={`flex flex-col gap-1 ${typeof className === 'string' ? className : ''}`}
        >
            <Rotulo texto={rotulo} obrigatorio={props.isRequired} />
            <div className="relative flex">
                <Input className={`${estiloEntrada} w-full pr-10 [&::-ms-reveal]:hidden`} />
                <Button
                    aria-label={visivel ? 'Ocultar senha' : 'Mostrar senha'}
                    aria-pressed={visivel}
                    onPress={() => setVisivel((v) => !v)}
                    className="absolute inset-y-0 right-0 flex w-10 items-center justify-center rounded-r-couro text-suave outline-none data-[hovered]:text-tinta data-[focus-visible]:ring-2 data-[focus-visible]:ring-acento"
                >
                    <Icone nome={visivel ? 'olhoRiscado' : 'olho'} />
                </Button>
            </div>
            {descricao && (
                <Text slot="description" className="text-xs text-suave">
                    {descricao}
                </Text>
            )}
            <FieldError className="text-sm text-vermelho" />
        </TextField>
    )
}
