import type { Key } from 'react-aria-components'
import { Button, ComboBox, FieldError, Input, ListBox, ListBoxItem, Popover, Text } from 'react-aria-components'
import { contem } from '../format/texto'
import { Icone } from './Icone'
import { Rotulo } from './Rotulo'

export interface Opcao {
    id: string
    rotulo: string
    detalhe?: string
}

const NENHUM = '__nenhum__'

/**
 * Lista de escolha em que dá para digitar e filtrar (pedido de 16/10), sem diferenciar maiúsculas nem acentos. Abre ao
 * focar ou clicar; Esc ou sair do campo sem escolher volta ao valor anterior. `vazio` é a opção "nenhum" (vira
 * `null`); sem ela, a escolha é obrigatória para mudar.
 */
export function CampoSelecao({
    rotulo,
    opcoes,
    valor,
    aoMudar,
    vazio,
    dica,
    descricao,
    name,
    isRequired = false,
    rotuloOculto = false,
    className = '',
}: {
    rotulo: string
    opcoes: Opcao[]
    valor: string | null
    aoMudar: (valor: string | null) => void
    vazio?: string
    /** Dica no campo vazio, quando o rótulo da opção vazia é longo demais para caber (padrão: o próprio `vazio`). */
    dica?: string
    descricao?: string
    name?: string
    isRequired?: boolean
    rotuloOculto?: boolean
    className?: string
}) {
    const itens: Opcao[] = vazio ? [{ id: NENHUM, rotulo: vazio }, ...opcoes] : opcoes
    return (
        <ComboBox
            name={name}
            defaultItems={itens}
            // Sem escolha, o campo fica vazio e mostra o rótulo da opção vazia como dica.
            selectedKey={valor}
            onSelectionChange={(chave: Key | null) => {
                // Apagar o texto e sair não muda a escolha: o ComboBox manda null e o campo volta ao valor atual.
                if (chave === null) return
                aoMudar(chave === NENHUM ? null : String(chave))
            }}
            defaultFilter={(texto, digitado) => contem(texto, digitado)}
            menuTrigger="focus"
            isRequired={isRequired}
            className={`flex flex-col gap-1 ${className}`}
        >
            <Rotulo texto={rotulo} obrigatorio={isRequired} oculto={rotuloOculto} />
            <div className="flex items-center rounded-couro border border-linha bg-superficie focus-within:outline-2 focus-within:outline-acento">
                <Input
                    placeholder={dica ?? vazio}
                    // Focar seleciona o texto: digitar já substitui a escolha atual pela busca.
                    onFocus={(e) => e.currentTarget.select()}
                    className="w-full min-w-0 bg-transparent px-3 py-2 text-tinta outline-none placeholder:text-suave"
                />
                <Button aria-label={`Abrir ${rotulo}`} className="px-2 py-2 text-suave">
                    <Icone nome="seta" className="size-3.5 rotate-90" />
                </Button>
            </div>
            {descricao && (
                <Text slot="description" className="text-xs text-suave">
                    {descricao}
                </Text>
            )}
            <FieldError className="text-sm text-vermelho" />
            <Popover className="max-h-72 min-w-[--trigger-width] overflow-y-auto rounded-couro border border-linha bg-superficie p-1 shadow-lg">
                <ListBox<Opcao>
                    className="outline-none"
                    renderEmptyState={() => <p className="px-3 py-2 text-sm text-suave">Nada encontrado.</p>}
                >
                    {(opcao) => (
                        <ListBoxItem
                            id={opcao.id}
                            textValue={opcao.rotulo}
                            className="flex cursor-default items-center justify-between gap-3 rounded-couro px-3 py-2 text-sm outline-none data-[focused]:bg-trilho data-[selected]:font-semibold"
                        >
                            {opcao.rotulo}
                            {opcao.detalhe && <span className="text-xs text-suave">{opcao.detalhe}</span>}
                        </ListBoxItem>
                    )}
                </ListBox>
            </Popover>
        </ComboBox>
    )
}
