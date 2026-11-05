import { useState } from 'react'
import { FieldError, Input, Text, TextField, ToggleButton, type TextFieldProps } from 'react-aria-components'
import { estiloEntrada } from './CampoTexto'
import { Icone } from './Icone'
import { Rotulo } from './Rotulo'

interface Props extends Omit<TextFieldProps, 'children' | 'type'> {
    rotulo: string
    descricao?: string
}

/**
 * Campo de senha com o botão de mostrar (o "olhinho"). Todo campo de senha usa este (convenções §4). O botão é um
 * `ToggleButton`: rótulo fixo "Mostrar senha" e o estado pelo `aria-pressed`; fica na ordem do Tab, depois do campo.
 * Visível, a senha continua fora do corretor e da maiúscula automática, e o botão nativo do Edge fica escondido para
 * não aparecerem dois.
 */
export function CampoSenha({ rotulo, descricao, className, ...props }: Props) {
    const [visivel, setVisivel] = useState(false)
    return (
        <TextField
            {...props}
            type={visivel ? 'text' : 'password'}
            className={`flex flex-col gap-1 ${typeof className === 'string' ? className : ''}`}
        >
            <Rotulo texto={rotulo} obrigatorio={props.isRequired} />
            <div className="relative flex">
                <Input
                    spellCheck={false}
                    autoCapitalize="off"
                    autoCorrect="off"
                    className={`${estiloEntrada} w-full pr-10 [&::-ms-reveal]:hidden`}
                />
                <ToggleButton
                    aria-label="Mostrar senha"
                    isSelected={visivel}
                    onChange={setVisivel}
                    className="absolute inset-y-0 right-0 flex w-10 items-center justify-center rounded-r-couro text-suave outline-none data-[hovered]:text-tinta data-[focus-visible]:ring-2 data-[focus-visible]:ring-acento"
                >
                    <Icone nome={visivel ? 'olhoRiscado' : 'olho'} />
                </ToggleButton>
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
