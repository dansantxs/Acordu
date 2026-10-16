import type { Key } from 'react-aria-components'
import {
    Button,
    FieldError,
    Label,
    ListBox,
    ListBoxItem,
    Popover,
    Select,
    SelectValue,
    Text,
} from 'react-aria-components'
import { Icone } from './Icone'

export interface Opcao {
    id: string
    rotulo: string
    detalhe?: string
}

/**
 * Lista de escolha com o visual dos campos. `vazio` é a opção "nenhum" (vira `null`); sem ela, a escolha é obrigatória.
 */
export function CampoSelecao({
    rotulo,
    opcoes,
    valor,
    aoMudar,
    vazio,
    descricao,
    name,
    rotuloOculto = false,
    className = '',
}: {
    rotulo: string
    opcoes: Opcao[]
    valor: string | null
    aoMudar: (valor: string | null) => void
    vazio?: string
    descricao?: string
    name?: string
    rotuloOculto?: boolean
    className?: string
}) {
    const NENHUM = '__nenhum__'
    const itens = vazio ? [{ id: NENHUM, rotulo: vazio }, ...opcoes] : opcoes
    return (
        <Select
            name={name}
            selectedKey={valor ?? (vazio ? NENHUM : null)}
            onSelectionChange={(chave: Key | null) =>
                aoMudar(chave === null || chave === NENHUM ? null : String(chave))
            }
            className={`flex flex-col gap-1 ${className}`}
        >
            <Label className={rotuloOculto ? 'sr-only' : 'text-sm font-semibold text-tinta'}>{rotulo}</Label>
            <Button className="flex items-center justify-between gap-2 rounded-couro border border-linha bg-superficie px-3 py-2 text-left text-tinta">
                <SelectValue className="truncate data-[placeholder]:text-suave" />
                <Icone nome="seta" className="size-3.5 rotate-90 text-suave" />
            </Button>
            {descricao && (
                <Text slot="description" className="text-xs text-suave">
                    {descricao}
                </Text>
            )}
            <FieldError className="text-sm text-vermelho" />
            <Popover className="max-h-72 min-w-[--trigger-width] overflow-y-auto rounded-couro border border-linha bg-superficie p-1 shadow-lg">
                <ListBox items={itens} className="outline-none">
                    {(opcao) => (
                        <ListBoxItem
                            id={opcao.id}
                            textValue={opcao.rotulo}
                            className="flex cursor-default items-center justify-between gap-3 rounded-couro px-3 py-2 text-sm outline-none data-[focused]:bg-trilho data-[selected]:font-semibold"
                        >
                            {opcao.rotulo}
                            {'detalhe' in opcao && opcao.detalhe && (
                                <span className="text-xs text-suave">{opcao.detalhe}</span>
                            )}
                        </ListBoxItem>
                    )}
                </ListBox>
            </Popover>
        </Select>
    )
}
